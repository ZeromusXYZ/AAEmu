// # define EXPORT_CELL_ON_LOAD
using System.Diagnostics;
using System.Numerics;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.IO;
using AAEmu.Game.Models.ClientData;
using AAEmu.Game.Models.CryEngine.Entities;
using AAEmu.Game.Models.CryEngine.Loaders;
using AAEmu.Game.Models.CryEngine.Objects;
using AAEmu.Game.Utils;
using Jitter2.LinearMath;
using NLog;

namespace AAEmu.Game.Models.Game.World;

public class WorldCell
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public WorldTemplate Template { get; init; }
    public int CellX { get; init; }
    public int CellY { get; init; }
    public bool Loaded { get; private set; }
    private readonly Lock _loadLock = new();
    private Vector3 CellOffset { get; set; }
    internal float[,] HeightMap { get; private set; }
    internal byte[,] MaterialsMap { get; private set; }
    private float MinHeight { get; set; }
    private float MaxHeight { get; set; }
    public Hmap LoadedHmap { get; private set; }
    public ObjectsFile LoadedObjectDat { get; set; }
    public VisAreasFile LoadedVisAreasDat { get; set; }
    public MaterialsFile MaterialFiles { get; set; }
    public MaterialsListFile MaterialListFiles { get; set; }
    public MaterialsFile StatObjsFiles { get; set; }

    /// <summary>
    /// Bai files data to use in this cell
    /// </summary>
    public BaseBaiLoader[,] BaiLoader { get; set; }

    /// <summary>
    /// Bounding box for use in Jitter
    /// </summary>
    public JBoundingBox BoundingBox { get; private set; }

    public string CellFolder => Path.Combine("game", "worlds", Template.Name, "cells", $"{CellX:000}_{CellY:000}");

    public WorldCell(int cellX, int cellY, WorldTemplate template)
    {
        CellX = cellX;
        CellY = cellY;
        Template = template;
        CellOffset = new Vector3(CellX * WorldManager.CELL_SIZE, CellY * WorldManager.CELL_SIZE, 0f);
        // Default bounding box
        BoundingBox = new JBoundingBox(
            new JVector(CellOffset.X, 0f, CellOffset.Y), 
            new JVector(CellOffset.X + WorldManager.CELL_SIZE, 0f, CellOffset.Y + WorldManager.CELL_SIZE)
            );
        BaiLoader = new BaseBaiLoader[,]
        {
            { null, null, null, null },
            { null, null, null, null },
            { null, null, null, null },
            { null, null, null, null }
        };
    }

    /// <summary>
    /// Load the *.bai files for this Cell if GeoDataMode is enabled 
    /// </summary>
    private void LoadBaiFiles()
    {
        if (!AppConfiguration.Instance.World.GeoDataMode)
            return; // Don't load navmesh if GeoDataMode is disabled

        // If we already loaded zone bai data for this world template, then don't try to load the cell one
        // Instead reference the zone bai directly
        if (Template.ZoneBaiLoader.Count > 0)
        {
            // This is zone grabbing is just an estimate, but should be good enough for this purpose
            // Ideally you'd check all 256 sectors in the cell and take it's median, but feels like overkill here
            var zoneKey = Template.ZoneKeyByRegions[CellX * WorldManager.SECTORS_PER_CELL, CellY * WorldManager.SECTORS_PER_CELL];
            if (Template.ZoneBaiLoader.TryGetValue(zoneKey, out var parentZoneBaiLoader))
            {
                // Assign it for all paths in this cell
                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        BaiLoader[x, y] = parentZoneBaiLoader;
                    }
                }
            }
            else
            {
                Logger.Warn($"WorldTemplate {Template.Name} has Zone bai files data loaded ({Template.ZoneBaiLoader.Count} zones), but could not find a matching file for this cell {zoneKey}");
            }
            return;
        }

        // Load the 4x4 grid of path folders into this cell
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                // var baiOffset = CellOffset + new Vector3(x * WorldManager.PATHS_SIZE, y * WorldManager.PATHS_SIZE, 0);
                var pathX = (uint)(CellX * 4 + x);
                var pathY = (uint)(CellY * 4 + y);
                var pathFolder = $"{pathX:000}_{pathY:000}";
                var pathBaiLoader = new BaseBaiLoader(Template);
                pathBaiLoader.LoadBaiFilesFromFolder(pathFolder); // (x != 0 || y != 0)
                BaiLoader[x, y] = pathBaiLoader;
                Template.PathBaiLoader.Add((pathX, pathY), pathBaiLoader);
                // TODO: Temporary disabled fake nodes until re-implemented
                // GenerateFakeIntermediateNodes(pathBaiLoader, baiOffset, pathX, pathY);
            }
        }
    }
    
    public long GenerateFakeIntermediateNodes(BaseBaiLoader baiLoader, Vector3 offset, uint pathX, uint pathY)
    {
        var timer = new Stopwatch();
        timer.Start();
        long newCount = 0;
        var stepSize = 6; // Size 6m Creates 43x43 points on empty region (1849)

        // First find the average height for all the netmission points in this .bai file
        // This will be used as our default "check locations" height. This is to avoid scanning from a zero-height position
        /*
        var averageHeight = 0f;
        var count = 0;
        foreach (var netMission in baiLoader.NetMissionReaders)
        {
            foreach (var node in netMission.NodeDescriptorList.Values)
            {
                averageHeight += node.Pos.Z;
                count++;
            }
        }
        if (count > 0)
        {
            averageHeight /= count;
        }
        */

        // new position, nearest old node found
        foreach (var netMission in baiLoader.NetMissionReaders)
        {
            var newNodesToAdd = new Dictionary<(int, int), NodeDescriptor>();
            var addedNodes = new Dictionary<(int, int), NodeDescriptor>();
            // Check "open spots" in the navmesh and mark them
            for (var y = 0; y < WorldManager.PATHS_SIZE; y += stepSize)
            {
                for (var x = 0; x < WorldManager.PATHS_SIZE; x += stepSize)
                {
                    var checkPos = offset + new Vector3(x, y, 0f);//averageHeight);
                    var nearestNode = baiLoader.FindClosestNetMissionNode(checkPos, 0, ignoreHeight: true);
                    // Check if the result it from the same NetMission file we want to check
                    if (nearestNode.NetMission != netMission)
                        continue; 
                    var distanceCheck = (checkPos - nearestNode.Pos).Length2D();
                    // var heightDifferenceCheck = Math.Abs(nearestNode.Pos.Z - checkPos.Z);
                    // If it's further than 2m away from the nearest node and not too steep, generate a new one
                    if (distanceCheck > stepSize * 2)// && (heightDifferenceCheck / (distanceCheck + 1f) < 0.5f))
                    {
                        newCount++;
                        newNodesToAdd.Add((x, y), nearestNode);
                    }
                }
            }

            // Actually add the points and their links to original nodes
            var lastUsedNodeId = netMission.NodeDescriptorList.Values.Max(x => x.Id);
            for (var y = 0; y < WorldManager.PATHS_SIZE; y += stepSize)
            {
                for (var x = 0; x < WorldManager.PATHS_SIZE; x += stepSize)
                {
                    if (!newNodesToAdd.TryGetValue((x, y), out var nearestNode))
                        continue;
                    lastUsedNodeId++;
                    var posToAdd = offset + new Vector3(x, y, nearestNode.Pos.Z); // copy over from nearest node
                    // Create new node
                    var newNode = new NodeDescriptor(netMission)
                    {
                        Id = lastUsedNodeId,
                        Pos = posToAdd,
                        Type = 99, // Mark them as custom floor
                    };
                    if (netMission.NodeDescriptorList.TryAdd(newNode.Id, newNode))
                        addedNodes.Add((x, y), newNode);

                    // Create new 2-directional link to nearest original node
                    netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { TargetNode = lastUsedNodeId, TargetNodeDescriptor = newNode, SourceNode = nearestNode.Id, SourceNodeDescriptor = nearestNode });
                    netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { SourceNode = lastUsedNodeId, SourceNodeDescriptor = newNode, TargetNode = nearestNode.Id, TargetNodeDescriptor = nearestNode });
                }
            }

            // Create cross-links between our newly generated node
            for (var y = 0; y < WorldManager.PATHS_SIZE-stepSize; y += stepSize)
            {
                for (var x = 0; x < WorldManager.PATHS_SIZE-stepSize; x += stepSize)
                {
                    if (!addedNodes.TryGetValue((x, y), out var newNode))
                        continue;
                    
                    // Just checking against Right and Bottom sides should be enough to handle the entire list of newly generated items
                    // Right
                    if (addedNodes.TryGetValue((x + stepSize, y), out var nodeR))
                    {
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { TargetNode = newNode.Id, TargetNodeDescriptor = newNode, SourceNode = nodeR.Id, SourceNodeDescriptor = nodeR});
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { SourceNode = newNode.Id, SourceNodeDescriptor = newNode, TargetNode = nodeR.Id, TargetNodeDescriptor = nodeR});
                    }
                    // Bottom (Top on map)
                    if (addedNodes.TryGetValue((x, y + stepSize), out var nodeB))
                    {
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { TargetNode = newNode.Id, TargetNodeDescriptor = newNode, SourceNode = nodeB.Id, SourceNodeDescriptor = nodeB});
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { SourceNode = newNode.Id, SourceNodeDescriptor = newNode, TargetNode = nodeB.Id, TargetNodeDescriptor = nodeB});
                    }
                    // Bottom-Right (Top-Right on map)
                    if (addedNodes.TryGetValue((x + stepSize, y + stepSize), out var nodeBr))
                    {
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { TargetNode = newNode.Id, TargetNodeDescriptor = newNode, SourceNode = nodeBr.Id, SourceNodeDescriptor = nodeBr});
                        netMission.LinkDescriptorList.Add(new LinkDescriptor(netMission) { SourceNode = newNode.Id, SourceNodeDescriptor = newNode, TargetNode = nodeBr.Id, TargetNodeDescriptor = nodeBr});
                    }
                }
            }
            if (pathX == 88 && pathY == 31)
            {
                // Debug here
            }

        }
        timer.Stop();
        if (newCount > 0 && timer.ElapsedMilliseconds > 100)
        {
            Logger.Debug($"Generate FakeIntermediateNodes: {newCount} generated nodes in {timer.ElapsedMilliseconds}ms for {Template.Name}, Cell {this}, Path {pathX:000}_{pathY:000}");
        }

        return newCount;
    }



    /// <summary>
    /// Checks if the cell is loaded and loads it if it hasn't 
    /// </summary>
    /// <returns></returns>
    public WorldCell VerifyCellLoaded()
    {
        if (Loaded)
            return this;

        lock (_loadLock)
        {
            Loading = true;
            // Assign heightmap array
            HeightMap = new float[WorldManager.CELL_HMAP_RESOLUTION, WorldManager.CELL_HMAP_RESOLUTION];
            MaterialsMap = new byte[WorldManager.CELL_HMAP_RESOLUTION, WorldManager.CELL_HMAP_RESOLUTION];
            // Load data
            LoadBaiFiles();
            Loaded = LoadCellDataFromClient();
            Loading = false;
        }
        return this;
    }

    /// <summary>
    /// Loads a given Cell worth of heightmap data
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private bool LoadCellDataFromClient()
    {
        var cellFolder = CellFolder;
        var heightMapFile = Path.Combine(cellFolder, "client", "terrain", "heightmap.dat");
        if (!ClientFileManager.FileExists(heightMapFile))
        {
            return true;
        }

        using var stream = ClientFileManager.GetFileStream(heightMapFile);
        if (stream == null)
        {
            return true;
        }

        // Logger.Debug($"Loading {heightMapFile}");

        // Read the cell hmap data
        using var br = new BinaryReader(stream);
        LoadedHmap = new Hmap();

        if (LoadedHmap.Read(br) < 0)
        {
            Logger.Error($"Error reading {heightMapFile}");
            return false;
        }

        LoadedHmap.SortNodes();

        // Read nodes into heightmap array
        #region ReadNodes

        MinHeight = float.MaxValue;
        MaxHeight = 0f;
        for (ushort sectorX = 0; sectorX < WorldManager.SECTORS_PER_CELL; sectorX++) // 16x16 sectors / cell
        for (ushort sectorY = 0; sectorY < WorldManager.SECTORS_PER_CELL; sectorY++)
        {
            var node = LoadedHmap.SortedNodes[sectorX * WorldManager.SECTORS_PER_CELL + sectorY];
            // var doubleValue = node.FRange * 100000d;

            for (ushort unitX = 0; unitX < WorldManager.SECTOR_HMAP_RESOLUTION; unitX++) // sector = 32x32 unit size
            for (ushort unitY = 0; unitY < WorldManager.SECTOR_HMAP_RESOLUTION; unitY++)
            {
                var oX = sectorX * WorldManager.SECTOR_HMAP_RESOLUTION + unitX;
                var oY = sectorY * WorldManager.SECTOR_HMAP_RESOLUTION + unitY;
                
                /*
                var rawValue = node.RawDataByIndex(unitX, unitY);
                var rawHeight = (ushort)(rawValue & NodeCell.HeightMapValueBits);
                var rawMaterial = (byte)(rawValue & NodeCell.HeightMapMaterialBits);
                MaterialsMap[oX, oY] = rawMaterial;

                var value = (ushort)(rawHeight * Template.HeightMaxCoefficient);

                HeightMap[oX, oY] = (ushort)(
                    doubleValue / 1.52604335620711 * Template.HeightMaxCoefficient / ushort.MaxValue * rawHeight +
                    node.BoxHeightmap.Min.Z * Template.HeightMaxCoefficient);
                */
                var value = node.HeightData[unitX, unitY];
                HeightMap[oX, oY] = value;
                MaterialsMap[oX, oY] = node.MaterialData[unitX, unitY];

                MinHeight = MathF.Min(value, MinHeight);
                MaxHeight = MathF.Max(value, MaxHeight);
            }
        }

        #endregion

        // Update cell bounding box
        BoundingBox = new JBoundingBox(
            new JVector(CellOffset.X, MinHeight, CellOffset.Y), 
            new JVector(CellOffset.X + WorldManager.CELL_SIZE, MaxHeight, CellOffset.Y + WorldManager.CELL_SIZE)
        );
        
        // Load Materials data
        var materialListFile = Path.Combine(cellFolder, "client", "material_list.dat");
        if (ClientFileManager.FileExists(materialListFile))
        {
            var materialList = new MaterialsListFile(materialListFile);
            materialList.ReadFile();
            MaterialListFiles = materialList;
        }

        var materialFile = Path.Combine(cellFolder, "client", "materials.dat");
        if (ClientFileManager.FileExists(materialFile))
        {
            var materials = new MaterialsFile(materialFile);
            materials.ReadFile();
            MaterialFiles = materials;
        }

        var statObjFile = Path.Combine(cellFolder, "client", "statobjs.dat");
        if (ClientFileManager.FileExists(statObjFile))
        {
            var statObjs = new MaterialsFile(statObjFile);
            statObjs.ReadFile();
            StatObjsFiles = statObjs;
        }

        // Load object.dat file
        var objectDatFile = Path.Combine(cellFolder, "client", "object.dat");
        if (ClientFileManager.FileExists(objectDatFile))
        {
            var objects = new ObjectsFile(objectDatFile);
            if (objects.ReadFile())
            {
                LoadedObjectDat = objects;
                // Logger.Debug($"Loaded objects from {objectDatFile}");
            }
            else
            {
                LoadedObjectDat = objects;
                if (objects.AssetPathsList.Count > 0 || objects.PrefabsList.Count > 0)
                    Logger.Error($"Error loading objects from {objectDatFile}, only {objects.AssetPathsList.Count} assets and {objects.PrefabsList.Count} prefabs read");
            }
        }

        // Load visareas.dat file
        var visAreasDatFile = Path.Combine(cellFolder, "client", "visareas.dat");
        if (ClientFileManager.FileExists(visAreasDatFile))
        {
            var visObjects = new VisAreasFile(visAreasDatFile);
            if (visObjects.ReadFile())
            {
                LoadedVisAreasDat = visObjects;
                // Logger.Debug($"Loaded objects from {objectDatFile}");
            }
            else
            {
                LoadedVisAreasDat = visObjects;
                if (visObjects.AssetPathsList.Count > 0 || visObjects.PrefabsList.Count > 0 || visObjects.VisAreas.Count > 0)
                    Logger.Error($"Error loading objects from {visAreasDatFile}, only {visObjects.AssetPathsList.Count} assets, {visObjects.PrefabsList.Count} prefabs and {visObjects.VisAreas.Count} visareas read");
            }
        }

        // Update Physics world's heightmaps
        foreach (var worldInstance in WorldManager.Instance.GetWorldsByTemplate(Template.Id).ToArray())
        {
            worldInstance.Physics?.UpdateHeightMapFromCellBody(this);
            // worldInstance.Physics?.AddHeightMapMeshFromCellBody(this);
            worldInstance.Water.AddFromCellData(this);
            // Add voxel terrain
            worldInstance.Physics?.AddStaticTerrainVoxels(this);
            // Add the remaining brush objects async
            worldInstance.QueueTerrainObjectsLoading(this);
            // worldInstance.Physics?.ReAlignLoadedBaiNodePoints(this);
        }
        
#if EXPORT_CELL_ON_LOAD
        if (CellX == 13 && CellY == 10) // Ezna
            ExportThisCell();
#endif
        return true;
    }

    /// <summary>
    /// Gets heightmap height at target data position, converted to float, but not smoothened
    /// </summary>
    /// <param name="heightMapDataX"></param>
    /// <param name="heightMapDataY"></param>
    /// <returns></returns>
    public float GetHeightMapDataInCell(int heightMapDataX, int heightMapDataY)
    {
        if (HeightMap == null ||
            heightMapDataX < 0 || heightMapDataX > WorldManager.CELL_HMAP_RESOLUTION ||
            heightMapDataY < 0 || heightMapDataY > WorldManager.CELL_HMAP_RESOLUTION)
        {
            return 0f; // out of bounds or not loaded
        }

        return HeightMap[heightMapDataX, heightMapDataY];
    }

    /// <summary>
    /// Gets heightmap height at target data position, converted to float, but not smoothened
    /// </summary>
    /// <param name="heightMapDataX"></param>
    /// <param name="heightMapDataY"></param>
    /// <returns></returns>
    public byte GetMaterialsDataInCell(int heightMapDataX, int heightMapDataY)
    {
        if (HeightMap == null ||
            heightMapDataX < 0 || heightMapDataX > WorldManager.CELL_HMAP_RESOLUTION ||
            heightMapDataY < 0 || heightMapDataY > WorldManager.CELL_HMAP_RESOLUTION)
        {
            return NodeCell.HeightMapMaterialHole; // out of bounds or not loaded, return as hole
        }

        return MaterialsMap[heightMapDataX, heightMapDataY];
    }

    /// <summary>
    /// Gets height at target world position
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>Returns 0 if it's outside of this cell's bounds, otherwise returns non-smoothened height</returns>
    public float GetHeight(int x, int y)
    {
        var xx = (int)(x - CellOffset.X) / 2;
        var yy = (int)(y - CellOffset.Y) / 2;
        return GetHeightMapDataInCell(xx, yy);
    }
    
#if EXPORT_CELL_ON_LOAD
    private void ExportThisCell()
    {
        var exportMaterialFileName = $"{Template.Name}_cell_{CellX:00}_{CellY:00}_material.data";
        var fs = new FileStream(exportMaterialFileName, FileMode.Create);
        for(var y = WorldManager.CELL_HMAP_RESOLUTION-1; y >= 0; y--)
        for (var x = 0; x < WorldManager.CELL_HMAP_RESOLUTION; x++)
        {
            var b = MaterialsMap[x, y];
            if (b == NodeCell.HeightMapMaterialHole)
                b = 0xff;
            fs.WriteByte(b);
        }
        fs.Close();
    }
#endif

    public Vector3 GetCellWorldOffset()
    {
        return new Vector3(CellX * WorldManager.CELL_SIZE, CellY * WorldManager.CELL_SIZE, 0f);
    }

    public override string ToString()
    {
        return $"{CellX:000}_{CellY:000}";
    }
}

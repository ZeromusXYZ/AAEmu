using System.Numerics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Models.CryEngine.Objects;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;

namespace AAEmu.Game.Models.Game.World;

public class WaterBodies
{
    [JsonIgnore]
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public float OceanLevel { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public List<WaterBodyArea> Areas { get; set; } = [];

    [JsonIgnore] public object _lock = new();

    /// <summary>
    /// Checks if a given point falls with a body of water
    /// </summary>
    /// <param name="point">Position to check</param>
    /// <param name="flowDirection">The direction the water is flowing if it has a flow</param>
    /// <returns></returns>
    public bool IsWater(Vector3 point, out Vector3 flowDirection)
    {
        flowDirection = Vector3.Zero;
        
        if (point.Z <= OceanLevel)
            return true;

        lock (_lock)
        {
            // TODO: take the top-most water area in case of overlaps
            foreach (var area in Areas)
            {
                if (area.GetSurface(point, out var surfacePoint, out flowDirection) &&
                    point.Z <= surfacePoint.Z &&
                    point.Z >= surfacePoint.Z - area.Depth)
                    return true;
            }

        }
        flowDirection = Vector3.Zero;
        return false;
    }

    /// <summary>
    /// Gets the surface height of a point within a body of water
    /// </summary>
    /// <param name="point">Position to check</param>
    /// <param name="flowDirection">The direction the water is flowing if it has a flow</param>
    /// <returns>Returns the surface height of the water body, with a minimum of ocean level</returns>
    public float GetWaterSurface(Vector3 point, out Vector3 flowDirection)
    {
        flowDirection = Vector3.Zero;
        
        if (point.Z <= OceanLevel)
            return OceanLevel;

        lock (_lock)
        {
            var closestHeight = 1000000f;
            foreach (var area in Areas)
                if (area.GetSurface(point, out var surfacePoint, out var f))
                {
                    var surfaceDistance = Math.Abs(surfacePoint.Z - point.Z);
                    if (surfaceDistance < closestHeight)
                    {
                        closestHeight = surfacePoint.Z;
                        flowDirection = f;
                    }
                    // return surfacePoint.Z;
                }

            if (closestHeight < 1000000f)
                return closestHeight;
        }

        return OceanLevel;
    }

    public static bool Save(string fileName, WaterBodies waterBodies)
    {
        try
        {
            lock (waterBodies._lock)
            {
                var jsonString = JsonConvert.SerializeObject(waterBodies, Formatting.Indented);
                File.WriteAllText(fileName, jsonString);
            }
        }
        catch
        {
            return false;
            // Ignore
        }
        return true;
    }

    public static bool Load(string fileName, out WaterBodies waterBodies)
    {
        waterBodies = null;
        try
        {
            var jsonString = File.ReadAllText(fileName);
            if (!JsonHelper.TryDeserializeObject<WaterBodies>(jsonString, out var newData, out _))
                return false;
            
            foreach (var area in newData.Areas)
            {
                // In effort to removing Height in favor of Depth, recalculate Z
                if (area.Height > 0f)
                {
                    area.Depth = area.Height;
                    area.Height = 0f;
                    for (var i = 0; i <= area.Points.Count - 1; i++)
                    {
                        var p = area.Points[i];
                        area.Points[i] = new Vector3(p.X, p.Y, p.Z + area.Depth);
                    }
                }
                
                // To fix issues with endpoints of rivers looping back to the start, remove the obsolete point from the data.
                // This doesn't really give an issue with water itself due to how it's handled, but is wrong nonetheless.
                if (area.AreaType == WaterBodyAreaType.LineArray && area.Points.Count > 2 && area.Points[^1].Equals(area.Points[0]))
                    area.Points.RemoveAt(area.Points.Count-1);
                
                area.UpdateBounds();
            }

            waterBodies = newData;
        }
        catch
        {
            return false;
            // Ignore
        }
        return true;
    }

    public uint GetNewId()
    {
        var res = 1000000u;
        
        foreach (var area in Areas)
        {
            if (area.Id >= res)
                res = area.Id + 1;
        }

        return res;
    }

    public void AddFromCellData(WorldCell worldCell)
    {
        var prefabIdx = 0;
        if (worldCell?.LoadedObjectDat == null)
            return;
        var cellOffset = worldCell.GetCellWorldOffset();
        foreach (var prefab in worldCell.LoadedObjectDat.PrefabsList)
        {
            prefabIdx++;
            if (prefab is not PrefabDataType11Water water)
                continue;

            // Does this water body have a border defined?
            // If yes, use its shape
            if (water.BorderPointsList.Count >= 2)
            {
                var newLake = new WaterBodyArea($"Water_C{worldCell.CellX}-{worldCell.CellY}_{prefabIdx}", WaterBodyAreaType.Polygon);
                newLake.Depth = water.Depth; // water.EndPos.Z - water.StartPos.Z;
                // TODO: check what the rest of DATA does before the vector array
                // There is likely information related to river directions and speed in there
                foreach (var v3 in water.BorderPointsList)
                {
                    var p = cellOffset + v3 with { Z = water.SurfaceHeight };
                    if (!newLake.Points.Contains(p)) // Filter the duplicates
                        newLake.Points.Add(p);
                }
                // Close the loop
                newLake.Points.Add(newLake.Points[0]);

                newLake.UpdateBounds();
                newLake.Speed = water.Speed;
                lock (_lock)
                {
                    newLake.Id = (uint)Areas.Count;
                    Areas.Add(newLake);
                }
            }
            else if (water.SegmentPointsList.Count >= 2)
            {
                // TODO: How to handle the in-shape values if border is not defined
                var newLake = new WaterBodyArea($"Segment_C{worldCell.CellX}-{worldCell.CellY}_{prefabIdx}", WaterBodyAreaType.Polygon);
                newLake.Depth = water.Depth; // water.EndPos.Z - water.StartPos.Z;
                // TODO: check what the rest of DATA does before the vector array
                // There is likely information related to river directions and speed in there
                foreach (var v3 in water.SegmentPointsList)
                {
                    var p = cellOffset + v3 with { Z = water.SurfaceHeight };
                    if (!newLake.Points.Contains(p)) // Filter the duplicates
                        newLake.Points.Add(p);
                }
                // Close the loop
                newLake.Points.Add(newLake.Points[0]);
                newLake.UpdateBounds();
                newLake.Speed = water.Speed;
                lock (_lock)
                {
                    newLake.Id = (uint)Areas.Count;
                    Areas.Add(newLake);
                }
            }
            else
            {
                Logger.Warn($"Water without data found at Cell {worldCell.CellX:000}-{worldCell.CellY:000} prefab Idx: {prefabIdx}");
            }

            // Logger.Debug($"Added {newLake.Name} with {newLake.BorderPoints.Count} points");
        }
    }
}

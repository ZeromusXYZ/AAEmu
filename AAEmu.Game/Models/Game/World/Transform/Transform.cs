﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Json;
using AAEmu.Game.Utils;
using Quartz.Listener;

// INFO
// https://www.versluis.com/2020/09/what-is-yaw-pitch-and-roll-in-3d-axis-values/
// https://en.wikipedia.org/wiki/Euler_angles
// https://gamemath.com/
// https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles

namespace AAEmu.Game.Models.Game.World.Transform
{

    /// <summary>
    /// Helper Class to help manipulating GameObjects positions in 3D space
    /// </summary>
    public class Transform : IDisposable
    {
        private GameObject _owningObject;
        private uint _worldId = WorldManager.DefaultWorldId ;
        private uint _instanceId = WorldManager.DefaultInstanceId;
        private uint _zoneId = 0;
        private PositionAndRotation _localPosRot;
        private Transform _parentTransform;
        private List<Transform> _children;
        private Transform _stickyParentTransform;
        private List<Transform> _stickyChildren;
        private Vector3 _lastFinalizePos = Vector3.Zero; // Might use this later for cheat detection or delta movement

        /// <summary>
        /// Parent Transform this Transform is attached to, leave null for World
        /// </summary>
        public Transform Parent { get => _parentTransform; set => SetParent(value); }
        /// <summary>
        /// List of Child Transforms of this Transform
        /// </summary>
        public List<Transform> Children { get => _children; }
        public Transform StickyParent { get => _stickyParentTransform; set => SetStickyParent(value); }
        /// <summary>
        /// List of Transforms that are linked to this object, but aren't direct children.
        /// Objects in this list need their positions updated when this object's local transform changes.
        /// Used for ladders on ships for example, only updates children if FinalizeTransform() is called
        /// FinalizeTransform takes the delta from previous call to calculate the delta movement
        /// </summary>
        public List<Transform> StickyChildren { get => _stickyChildren; }
        /// <summary>
        /// The GameObject this Transform is attached to
        /// </summary>
        public GameObject GameObject { get => _owningObject; }
        /// <summary>
        /// World ID
        /// </summary>
        public uint WorldId { get => _worldId; set => _worldId = value; }
        /// <summary>
        /// Instance ID
        /// </summary>
        public uint InstanceId { get => _instanceId; set => _instanceId = value; }
        /// <summary>
        /// Zone ID (Key)
        /// </summary>
        public uint ZoneId { get => _zoneId; set => _zoneId = value; }
        /// <summary>
        /// The Local Transform information (relative to Parent)
        /// </summary>
        public PositionAndRotation Local { get => _localPosRot; }
        /// <summary>
        /// The Global Transform information (relative to game world)
        /// </summary>
        public PositionAndRotation World { get => GetWorldPosition(); }
        // TODO: It MIGHT be interesting to cache the world Transform, but would generate more overhead when moving parents (vehicles/mounts)

        private void InternalInitializeTransform(GameObject owningObject, Transform parentTransform = null)
        {
            _owningObject = owningObject;
            _parentTransform = parentTransform;
            _children = new List<Transform>();
            _localPosRot = new PositionAndRotation();
            _stickyParentTransform = null;
            _stickyChildren = new List<Transform>();
        }

        public Transform(GameObject owningObject, Transform parentTransform)
        {
            InternalInitializeTransform(owningObject, parentTransform);
        }

        public Transform(GameObject owningObject, Transform parentTransform, float x, float y, float z)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            Local.Position = new Vector3(x, y, z);
        }

        public Transform(GameObject owningObject, Transform parentTransform, Vector3 position)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            Local.Position = position;
        }

        public Transform(GameObject owningObject, Transform parentTransform, float posX, float posY, float posZ, float roll, float pitch, float yaw)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            Local.Position = new Vector3(posX, posY, posZ);
            Local.Rotation = new Vector3(roll, pitch, yaw);
        }

        public Transform(GameObject owningObject, Transform parentTransform, Vector3 position, Vector3 rotation)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            Local.Position = position;
            Local.Rotation = rotation;
        }

        public Transform(GameObject owningObject, Transform parentTransform, uint worldId, uint zoneId, uint instanceId, float posX, float posY, float posZ, float roll, float pitch, float yaw)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            WorldId = worldId;
            ZoneId = zoneId;
            InstanceId = instanceId;
            Local.Position = new Vector3(posX, posY, posZ);
            Local.Rotation = new Vector3(roll, pitch, yaw);
        }

        public Transform(GameObject owningObject, Transform parentTransform, uint worldId, uint zoneId, uint instanceId, float posX, float posY, float posZ, float yaw)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            WorldId = worldId;
            ZoneId = zoneId;
            InstanceId = instanceId;
            Local.Position = new Vector3(posX, posY, posZ);
            Local.Rotation = new Vector3(0f, 0f, yaw);
        }

        public Transform(GameObject owningObject, Transform parentTransform, uint worldId, uint zoneId, uint instanceId, PositionAndRotation posRot)
        {
            InternalInitializeTransform(owningObject, parentTransform);
            WorldId = worldId;
            ZoneId = zoneId;
            InstanceId = instanceId;
            _localPosRot = new PositionAndRotation(posRot.Position, posRot.Rotation);
        }

        /// <summary>
        /// Clones a Transform including GameObject and Parent Transform information
        /// </summary>
        /// <returns></returns>
        public Transform Clone()
        {
            return new Transform(_owningObject, _parentTransform, WorldId, ZoneId, InstanceId, _localPosRot);
        }
        
        /// <summary>
        /// Clones a Transform, keeps the parent Transform set, but replaces owning object with newOwner
        /// </summary>
        /// <param name="newOwner"></param>
        /// <returns></returns>
        public Transform Clone(GameObject newOwner)
        {
            return new Transform(newOwner, _parentTransform, WorldId, ZoneId, InstanceId, _localPosRot);
        }

        /// <summary>
        /// Clones a Transform without GameObject or Parent Transform, using the current World relative position
        /// </summary>
        /// <returns></returns>
        public Transform CloneDetached()
        {
            return new Transform(null, null, WorldId, ZoneId, InstanceId, GetWorldPosition());
        }

        /// <summary>
        /// Clones a Transform without Parent Transform but with newOwner as new owner, using the current World relative position
        /// </summary>
        /// <param name="newOwner"></param>
        /// <returns></returns>
        public Transform CloneDetached(GameObject newOwner)
        {
            return new Transform(newOwner, null, WorldId, ZoneId, InstanceId, GetWorldPosition());
        }

        /// <summary>
        /// Clones a Transform using childObject as new owner and setting Parent Transform to the current transform, the new clone has a local position initialized as 0,0,0
        /// </summary>
        /// <returns></returns>
        public Transform CloneAttached(GameObject childObject)
        {
            return new Transform(childObject, this, WorldId, ZoneId, InstanceId, new PositionAndRotation());
        }

        /// <summary>
        /// Clones the current World Transform into a WorldSpawnPosition object
        /// </summary>
        /// <returns></returns>
        public WorldSpawnPosition CloneAsSpawnPosition()
        {
            return new WorldSpawnPosition()
            {
                WorldId = this.WorldId,
                ZoneId = this.ZoneId,
                X = this.World.Position.X,
                Y = this.World.Position.Y,
                Z = this.World.Position.Z,
                Roll = this.World.Rotation.X,
                Pitch = this.World.Rotation.Y,
                Yaw = this.World.Rotation.Z
            };
        }

        ~Transform()
        {
            DetachAll();
        }

        public void Dispose()
        {
            DetachAll();
        }

        /// <summary>
        /// Detaches this Transform from it's Parent, and detaches all it's children. Children get their World Transform as Local
        /// </summary>
        public void DetachAll()
        {
            Parent = null;
            foreach (var child in Children)
                child.Parent = null;
        }

        /// <summary>
        /// Assigns a new Parent Transform, automatically handles related child Transforms
        /// </summary>
        /// <param name="parent"></param>
        protected void SetParent(Transform parent)
        {
            if ((parent == null) || (!parent.Equals(_parentTransform)))
            {
                // Detach sticky
                //if (_parentlessParentTransform != null)
                //    this._parentlessParentTransform.DetachParentlessTransform(this);
                
                if (_parentTransform != null)
                    _parentTransform.InternalDetachChild(this);

                if ((_owningObject != null) && (_owningObject is Character player))
                {
                    var oldS = "<null>";
                    var newS = "<null>";
                    if ((_parentTransform != null) && (_parentTransform._owningObject is BaseUnit oldParentUnit))
                    {
                        oldS = oldParentUnit.Name;
                        if (oldS == string.Empty)
                            oldS = oldParentUnit.ToString();
                        oldS += " (" + oldParentUnit.ObjId +")";
                    }
                    if ((parent != null) && (parent._owningObject is BaseUnit newParentUnit))
                    {
                        newS = newParentUnit.Name;
                        if (newS == string.Empty)
                            newS = newParentUnit.ToString();
                        newS += " (" + newParentUnit.ObjId +")";
                    }

                    if (_parentTransform?._owningObject != parent?._owningObject)
                        player.SendMessage("|cFF88FF88Changing parent - {0} => {1}|r", oldS, newS);
                }
                _parentTransform = parent;

                if (_parentTransform != null)
                    _parentTransform.InternalAttachChild(this);
            }
        }

        private void InternalAttachChild(Transform child)
        {
            if (!_children.Contains(child))
            {
                _children.Add(child);
                // TODO: This needs better handling and take into account rotations
                child.Local.Position -= Local.Position;
                child.GameObject.ParentObj = this.GameObject;
            }
        }

        private void InternalDetachChild(Transform child)
        {
            if (_children.Contains(child))
            {
                _children.Remove(child);
                // TODO: This needs better handling and take into account rotations
                child.Local.Position += Local.Position;
                child.GameObject.ParentObj = null;
            }
        }

        /// <summary>
        /// Calculates and returns a Transform by processing all underlying parents
        /// </summary>
        /// <returns></returns>
        private PositionAndRotation GetWorldPosition()
        {
            if (_parentTransform == null)
                return _localPosRot;
            var res = _parentTransform.GetWorldPosition().Clone();

            // TODO: This is not taking into account parent rotation !
            res.Translate(Local.Position);
            res.Rotate(Local.Rotation);
            // Is this even correct ?
           
            res.IsLocal = false;
            return res;
        }

        /// <summary>
        /// Detaches the transform, and sets the Local Position and Rotation to what is defined in the WorldSpawnPosition
        /// </summary>
        /// <param name="wsp">WorldSpawnPosition to copy information from</param>
        /// <param name="newInstanceId">new InstanceId to assign to this transform, unchanged if 0</param>
        public void ApplyWorldSpawnPosition(WorldSpawnPosition wsp,uint newInstanceId = 0)
        {
            DetachAll();
            WorldId = wsp.WorldId;
            ZoneId = wsp.ZoneId;
            if (newInstanceId != 0)
                InstanceId = newInstanceId;
            Local.Position = new Vector3(wsp.X, wsp.Y, wsp.Z);
            Local.Rotation = new Vector3(wsp.Roll, wsp.Pitch, wsp.Yaw);
        }

        /// <summary>
        /// Delegates the current position and rotation to the owning GameObject.SetPosition() function
        /// </summary>
        public void FinalizeTransform(bool includeChildren = true)
        {
            var worldPosDelta = World.ClonePosition() - _lastFinalizePos;
            // TODO: Check if/make sure rotations are taken into account
            if (StickyChildren.Count > 0)
            {
                foreach (var stickyChild in StickyChildren)
                {
                    stickyChild.Local.Translate(worldPosDelta);
                    WorldManager.Instance.AddVisibleObject(stickyChild.GameObject);

                    if (!(stickyChild.GameObject is Unit))
                        continue;

                    
                    // Create a moveType
                    /*
                    var mt = new UnitMoveType();
                    var wPos = stickyChild.World.Clone();
                    //mt.Flags = 0x00;
                    mt.Flags = 0x40; // sticky/attached
                    mt.X = wPos.Position.X;
                    mt.Y = wPos.Position.Y;
                    mt.Z = wPos.Position.Z;
                    var (r, p, y) = wPos.ToRollPitchYawSBytesMovement();
                    mt.DeltaMovement[1] = 127;
                    mt.RotationX = r;
                    mt.RotationY = p;
                    mt.RotationZ = y;
                    mt.ActorFlags = 0x40; // sticky/climbing
                    mt.ClimbData = 1; // ladder is sticky ?
                    mt.Stance = 6;
                    
                    // Related to object we're sticking to
                    // First 13 bits is for vertical offset (Z)
                    // Next 8 bits is for horizontal offset (Y?)
                    // upper 8 bits is 0x7F when sticking to a vine or ladder, this might possibly be the depth (X?)
                    mt.GcId = 0; 
                    stickyChild.GameObject.BroadcastPacket(
                        new SCOneUnitMovementPacket(stickyChild.GameObject.ObjId, mt),
                        false);
                    */
                }
            }

            _lastFinalizePos = World.ClonePosition();
            if (_owningObject == null)
                return;
            
            if (!_owningObject.DisabledSetPosition)
                WorldManager.Instance.AddVisibleObject(_owningObject);

            if (_owningObject is Slave slave)
            {
                foreach (var dood in slave.AttachedDoodads)
                    WorldManager.Instance.AddVisibleObject(dood);
                foreach (var chld in slave.AttachedSlaves)
                    WorldManager.Instance.AddVisibleObject(chld);
                /*
                foreach (var objs in slave.AttachedCharacters)
                    WorldManager.Instance.AddVisibleObject(objs.Value);
                */
            }

            if (includeChildren)
                foreach (var child in _children)
                    child.FinalizeTransform();

            //_owningObject.SetPosition(Local.Position.X,Local.Position.Y,Local.Position.Z,Local.Rotation.X,Local.Rotation.Y,Local.Rotation.Z);
        }

        /// <summary>
        /// Returns a summary of the current local location and parent objects if this is a child
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToFullString(true, false);
        }
        public string ToFullString(bool isFirstInList = true,bool chatFormatted = false)
        {
            var chatColorWhite = chatFormatted ? "|cFFFFFFFF" : "";
            var chatColorGreen = chatFormatted ? "|cFF00FF00" : "";
            var chatColorYellow = chatFormatted ? "|cFFFFFF00" : "";
            var chatColorRestore = chatFormatted ? "|r" : "";
            var chatLineFeed = chatFormatted ? "\n" : "";
            var res = string.Empty;
            if (isFirstInList && ((_parentTransform != null) || (_stickyParentTransform != null)))
                res += "[" + chatColorWhite + World.ToString() + chatColorRestore + "] " + chatLineFeed + "=> "; 
            res += Local.ToString();
            if (_parentTransform != null)
            {
                res += "\n on ( ";
                if (_parentTransform._owningObject is BaseUnit bu)
                {
                    if (bu.Name != string.Empty)
                        res += chatColorGreen + bu.Name + chatColorRestore + " ";
                    res += "#" + chatColorWhite + bu.ObjId + chatColorRestore + " ";
                }

                res += _parentTransform.ToFullString(false, chatFormatted);
                res += " )" + chatLineFeed;
            }

            if (_stickyParentTransform != null)
            {
                res += "\n sticking to ( ";
                if (_stickyParentTransform._owningObject is BaseUnit bu)
                {
                    if (bu.Name != string.Empty)
                        res += chatColorYellow + bu.Name + chatColorRestore + " ";
                    res += "#" + chatColorWhite + bu.ObjId + chatColorRestore + " ";
                }

                res += _stickyParentTransform.ToFullString(false, chatFormatted);
                res += " )" + chatLineFeed;
            }
            return res;
        }

        /// <summary>
        /// Add child to StickyChildren list, these children are not included in parent/child relations, but are updated with delta movements
        /// </summary>
        /// <param name="stickyChild"></param>
        /// <returns>Returns true if successfully attached, or false if already attached or other errors</returns>
        public bool AttachStickyTransform(Transform stickyChild)
        {
            // NUll-check
            if ((stickyChild == null) || (stickyChild.GameObject == null))
                return false;
            // Check if already there
            if (StickyChildren.Contains(stickyChild))
                return false;
            // Check if in the same world
            if ((stickyChild.WorldId != this.WorldId) || (stickyChild.InstanceId != this.InstanceId))
                return false;
            StickyChildren.Add(stickyChild);
            stickyChild._stickyParentTransform = this;
            return true;
        }

        /// <summary>
        /// Detaches child from StickyChildren list, and sets the child's stickyParent to null
        /// </summary>
        /// <param name="stickyChild"></param>
        public void DetachStickyTransform(Transform stickyChild)
        {
            if (StickyChildren.Contains(stickyChild))
                _stickyChildren.Remove(stickyChild);
            stickyChild._stickyParentTransform = null;
        }

        protected void SetStickyParent(Transform stickyParent)
        {
            var oldParent = _stickyParentTransform;
            // Detach from previous sticky parent if needed 
            if ((_stickyParentTransform != null) && (_stickyParentTransform != stickyParent))
                _stickyParentTransform.DetachStickyTransform(this);
            
            if (GameObject is Character player)
                if (oldParent != stickyParent)
                {
                    var oldS = "<null>";
                    var newS = "<null>";
                    if ((oldParent != null) && (oldParent._owningObject is BaseUnit oldParentUnit))
                    {
                        oldS = oldParentUnit.Name;
                        if (oldS == string.Empty)
                            oldS = oldParentUnit.ToString();
                        oldS += " (" + oldParentUnit.ObjId +")";
                    }
                    if ((stickyParent != null) && (stickyParent._owningObject is BaseUnit newParentUnit))
                    {
                        newS = newParentUnit.Name;
                        if (newS == string.Empty)
                            newS = newParentUnit.ToString();
                        newS += " (" + newParentUnit.ObjId +")";
                    }

                    player.SendMessage("|cFFFF88FFChanging Sticky - {0} => {1}|r", oldS, newS);                    
                }

            
            // Attach to new parent if needed
            if (stickyParent != null)
                stickyParent.AttachStickyTransform(this);
        }

    }
}
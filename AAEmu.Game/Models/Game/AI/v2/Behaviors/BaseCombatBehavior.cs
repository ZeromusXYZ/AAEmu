﻿using System;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors
{
    public abstract class BaseCombatBehavior : Behavior
    {
        protected DateTime _delayEnd;
        protected bool _strafeDuringDelay;
        
        public void MoveInRange(BaseUnit target, float range, float speed)
        {
            var distanceToTarget = Ai.Owner.GetDistanceTo(target, true);
            // var distanceToTarget = MathUtil.CalculateDistance(Ai.Owner.Position, target.Position, true);
            if (distanceToTarget > range)
                Ai.Owner.MoveTowards(target.Position, speed);
            else 
                Ai.Owner.StopMovement();
        }

        protected bool CanStrafe 
        {
            get
            {
                return DateTime.UtcNow > _delayEnd || _strafeDuringDelay;
            }
        }
        
        protected bool CanUseSkill
        {
            get
            {
                if (Ai.Owner.SkillTask != null || Ai.Owner.ActivePlotState != null)
                    return false;
                return DateTime.UtcNow >= _delayEnd && !Ai.Owner.IsGlobalCooldowned;
            }
        }
        
        // UseSkill (delay)
        public void UseSkill(Skill skill, BaseUnit target)
        {
            var skillCaster = SkillCaster.GetByType(SkillCasterType.Unit);
            skillCaster.ObjId = Ai.Owner.ObjId;

            SkillCastTarget skillCastTarget;
            switch (skill.Template.TargetType)
            {
                case SkillTargetType.Pos:
                    var pos = Ai.Owner.Position;
                    skillCastTarget = new SkillCastPositionTarget()
                    {
                        ObjId = Ai.Owner.ObjId,
                        PosX = pos.X,
                        PosY = pos.Y,
                        PosZ = pos.Z,
                        PosRot = (float)MathUtil.ConvertDirectionToDegree(pos.RotationZ) //Is this rotation right?
                    };
                    break;
                default:
                    skillCastTarget = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
                    skillCastTarget.ObjId = target.ObjId;
                    break;
            }

            var skillObject = SkillObject.GetByType(SkillObjectType.None);

            //Run this in a task maybe?
            skill.Use(Ai.Owner, skillCaster, skillCastTarget, skillObject);
        }
        
        // Check if can pick a new skill (delay, already casting)
    }
}

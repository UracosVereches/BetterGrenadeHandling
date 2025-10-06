using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace BetterGrenadeHandling
{
    //Distance = Length(Target_Position - Firing_Position)
    //Time = Distance / Bullet_Speed
    //Predicted_Position = Target_Position + (Target_Velocity * Time)
    public static class TrajectoryPrediction
    {
        private static Projectile projectile;
        
        private static void test()
        {
            projectile.def.projectile.Speed
        }
    }
}

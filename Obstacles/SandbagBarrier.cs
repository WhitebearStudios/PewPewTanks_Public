using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandbagBarrier : Obstacle
{
    protected override Vector2 Size { get => new Vector2(1.44f, 0.73f); }

    public override bool FitToSlope { get => false; }
}

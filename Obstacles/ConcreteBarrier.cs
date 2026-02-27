using UnityEngine;

public class ConcreteBarrier : Obstacle
{
    protected override Vector2 Size { get => new Vector2(0.4f, 1.2f); }

    public override bool FitToSlope { get => false; }
}

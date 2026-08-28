using Godot;

/// <summary>
/// Builds the cafeteria foreground from an RGBA atlas. Each source region is
/// positioned at the same coordinates as the background, while its Node2D
/// origin defines the Y-sort depth. The atlas contains only tightly cut object
/// pixels: floor, rugs, empty wall areas and cast-shadow rectangles are fully
/// transparent.
/// </summary>
public partial class CafeteriaYSortOccluders : Node2D
{
    [Export]
    public Texture2D SourceTexture { get; set; }

    public override void _Ready()
    {
        if (SourceTexture == null)
        {
            GD.PushError(
                "CafeteriaYSortOccluders requires the transparent cafeteria atlas."
            );
            return;
        }

        BuildDiningRoom();
        BuildServiceArea();
        BuildKitchen();
        BuildOffice();
        BuildEntrance();
    }

    private void BuildDiningRoom()
    {
        AddOccluder("BoothBack", 40, 43, 246, 68, 105);
        AddOccluder("BoothTables", 40, 111, 246, 57, 150);

        AddOccluder("DiningTableUpperLeft", 108, 176, 90, 91, 228);
        AddOccluder("DiningTableUpperRight", 207, 176, 76, 90, 228);
        AddOccluder("DiningTableLowerLeft", 105, 280, 98, 102, 335);
        AddOccluder("DiningTableLowerMiddle", 211, 280, 104, 102, 335);
        AddOccluder("DiningTableCenter", 321, 248, 100, 112, 310);
        AddOccluder("DiningTableRight", 445, 257, 110, 113, 320);

        AddOccluder("LeftWallPlant", 34, 155, 67, 85, 215);
        AddOccluder("LeftBookshelf", 35, 238, 73, 105, 315);
        AddOccluder("LowerLeftPlanter", 34, 345, 81, 81, 404);
    }

    private void BuildServiceArea()
    {
        AddOccluder("EspressoStation", 327, 94, 98, 49, 142);
        AddOccluder("PastryDisplay", 276, 143, 201, 65, 198);
        AddOccluder("RegisterCounterTop", 477, 143, 113, 65, 204);
        AddOccluder("CounterFront", 276, 208, 314, 36, 232);
    }

    private void BuildKitchen()
    {
        AddOccluder("KitchenIsland", 588, 144, 121, 72, 198);
    }

    private void BuildOffice()
    {
        AddOccluder("OfficeFurniture", 570, 218, 160, 114, 300);
    }

    private void BuildEntrance()
    {
        AddOccluder("EntranceFrameAndPlants", 272, 365, 222, 109, 450);
    }

    private void AddOccluder(
        string name,
        float x,
        float y,
        float width,
        float height,
        float sortY
    )
    {
        Node2D group = new()
        {
            Name = name,
            Position = new Vector2(0, sortY)
        };

        Vector2[] sourcePoints =
        {
            new(x, y),
            new(x + width, y),
            new(x + width, y + height),
            new(x, y + height)
        };

        Vector2[] localPoints = new Vector2[sourcePoints.Length];

        for (int index = 0; index < sourcePoints.Length; index++)
            localPoints[index] = sourcePoints[index] - group.Position;

        Polygon2D sprite = new()
        {
            Name = "TransparentSprite",
            Polygon = localPoints,
            UV = sourcePoints,
            Texture = SourceTexture
        };

        group.AddChild(sprite);
        AddChild(group);
    }
}

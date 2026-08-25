using Godot;
using System.Collections.Generic;

/// <summary>
/// Builds the asymmetric office foreground from a binary-alpha atlas. Every
/// sampled object uses a collision-aligned Y-sort origin; neutral floor and
/// cast-shadow pixels stay in the background. Overlapping source rectangles
/// are partitioned so the same pixels are never rendered at two depths.
/// </summary>
public partial class OfficeYSortOccluders : Node2D
{
    [Export]
    public Texture2D SourceTexture { get; set; }

    private readonly List<SourceRegion> _claimedSourceRegions = new();

    public override void _Ready()
    {
        if (SourceTexture == null)
        {
            GD.PushError(
                "OfficeYSortOccluders requires the transparent office atlas."
            );
            return;
        }

        BuildServerRoom();
        BuildMeetingRoom();
        BuildOpenWorkspace();
        BuildReception();
        BuildEntrance();
    }

    private void BuildServerRoom()
    {
        AddOccluder("ServerPlant", 46, 99, 29, 47, 139);
        AddOccluder("ServerFrontWall", 40, 134, 405, 38, 163);
        AddOccluder("ServerRackRow", 65, 61, 341, 81, 134);
    }

    private void BuildMeetingRoom()
    {
        AddOccluder("MeetingBookshelf", 678, 37, 31, 70, 80);
        AddOccluder("MeetingTableAndChairs", 551, 78, 136, 96, 166);
        AddOccluder("MeetingFrontWall", 513, 187, 202, 19, 199);
    }

    private void BuildOpenWorkspace()
    {
        AddOccluder("WorkClusterLeft", 63, 198, 108, 96, 285);
        AddOccluder("WorkClusterUpper", 208, 198, 86, 96, 285);
        AddOccluder("CentralPlant", 322, 294, 42, 55, 341);
        AddOccluder("WorkClusterLower", 360, 304, 102, 101, 397);
        AddOccluder("OperationsStation", 333, 198, 109, 69, 260);
        AddOccluder("AnalystDesk", 151, 349, 160, 79, 420);
        AddOccluder("KnowledgeShelf", 34, 302, 71, 62, 338);
        AddOccluder("EquipmentCabinet", 72, 388, 69, 52, 432);
        AddOccluder("LeftServicePlant", 21, 229, 39, 72, 292);
        AddOccluder("LowerPlantLeft", 23, 367, 31, 54, 413);
        AddOccluder("LowerPlantRight", 478, 377, 39, 59, 428);
    }

    private void BuildReception()
    {
        AddOccluder("ReceptionWallShelf", 558, 222, 154, 65, 244);
        AddOccluder("VisitorChairs", 573, 337, 119, 45, 364);
        AddOccluder("ReceptionPlant", 683, 270, 29, 57, 290);
        AddOccluder("ReceptionFloorPlant", 711, 351, 41, 57, 399);
        AddOccluder("ReceptionCounter", 556, 270, 158, 84, 320);
    }

    private void BuildEntrance()
    {
        AddOccluder("EntrancePlantLeft", 548, 431, 35, 61, 484);
        AddOccluder("EntrancePlantRight", 643, 431, 36, 61, 484);
        AddOccluder("EntranceThreshold", 579, 487, 70, 25, 503);
        AddOccluder("EntranceDoor", 583, 423, 63, 69, 483);
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
        SourceRegion requestedRegion = new(x, y, width, height);
        List<SourceRegion> visibleRegions = new() { requestedRegion };

        foreach (SourceRegion claimedRegion in _claimedSourceRegions)
        {
            List<SourceRegion> remainingRegions = new();

            foreach (SourceRegion visibleRegion in visibleRegions)
                remainingRegions.AddRange(
                    SubtractRegion(visibleRegion, claimedRegion)
                );

            visibleRegions = remainingRegions;

            if (visibleRegions.Count == 0)
                break;
        }

        _claimedSourceRegions.Add(requestedRegion);

        Node2D group = new()
        {
            Name = name,
            Position = new Vector2(0, sortY)
        };

        int partNumber = 1;

        foreach (SourceRegion visibleRegion in visibleRegions)
        {
            AddSpritePart(group, visibleRegion, partNumber);
            partNumber++;
        }

        AddChild(group);
    }

    private void AddSpritePart(
        Node2D group,
        SourceRegion sourceRegion,
        int partNumber
    )
    {
        float left = sourceRegion.Left;
        float top = sourceRegion.Top;
        float right = sourceRegion.Right;
        float bottom = sourceRegion.Bottom;

        Vector2[] sourcePoints =
        {
            new(left, top),
            new(right, top),
            new(right, bottom),
            new(left, bottom)
        };

        Vector2[] localPoints = new Vector2[sourcePoints.Length];

        for (int index = 0; index < sourcePoints.Length; index++)
            localPoints[index] = sourcePoints[index] - group.Position;

        Polygon2D sprite = new()
        {
            Name = $"TransparentSprite{partNumber:D2}",
            Polygon = localPoints,
            UV = sourcePoints,
            Texture = SourceTexture
        };

        group.AddChild(sprite);
    }

    private static List<SourceRegion> SubtractRegion(
        SourceRegion source,
        SourceRegion claimed
    )
    {
        float overlapLeft = Mathf.Max(source.Left, claimed.Left);
        float overlapTop = Mathf.Max(source.Top, claimed.Top);
        float overlapRight = Mathf.Min(source.Right, claimed.Right);
        float overlapBottom = Mathf.Min(source.Bottom, claimed.Bottom);

        if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
            return new List<SourceRegion> { source };

        List<SourceRegion> result = new();

        AddRegionIfVisible(
            result,
            source.Left,
            source.Top,
            source.Right,
            overlapTop
        );
        AddRegionIfVisible(
            result,
            source.Left,
            overlapBottom,
            source.Right,
            source.Bottom
        );
        AddRegionIfVisible(
            result,
            source.Left,
            overlapTop,
            overlapLeft,
            overlapBottom
        );
        AddRegionIfVisible(
            result,
            overlapRight,
            overlapTop,
            source.Right,
            overlapBottom
        );

        return result;
    }

    private static void AddRegionIfVisible(
        List<SourceRegion> regions,
        float left,
        float top,
        float right,
        float bottom
    )
    {
        if (right > left && bottom > top)
            regions.Add(new SourceRegion(left, top, right - left, bottom - top));
    }

    private readonly struct SourceRegion
    {
        public SourceRegion(float x, float y, float width, float height)
        {
            Left = x;
            Top = y;
            Right = x + width;
            Bottom = y + height;
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
    }
}

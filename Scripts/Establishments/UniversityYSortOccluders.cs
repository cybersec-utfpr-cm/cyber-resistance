using Godot;

/// <summary>
/// Reuses exact pieces of the university foreground atlas as Y-sortable
/// occluders. The atlas has a real alpha channel, so the floor between legs,
/// chairs, leaves and door frames is never redrawn over the player.
/// </summary>
public partial class UniversityYSortOccluders : Node2D
{
    [Export]
    public Texture2D SourceTexture { get; set; }

    public override void _Ready()
    {
        if (SourceTexture == null)
        {
            GD.PushError("UniversityYSortOccluders requires the transparent university occluder atlas.");
            return;
        }

        BuildClassroom();
        BuildComputerLab();
        BuildProfessorRoom();
        BuildExamRoom();
        BuildEntrance();
    }

    private void BuildClassroom()
    {
        AddSolidRectangle("ClassroomBookshelf", 45, 47, 70, 43, 68);
        AddSolidRectangle("ClassroomCabinet", 235, 52, 31, 47, 76);
        AddSolidRectangle("TeacherDesk", 156, 76, 41, 33, 91);

        AddClassDesk("ClassDesk01", 84, 126);
        AddClassDesk("ClassDesk02", 144, 126);
        AddClassDesk("ClassDesk03", 203, 126);
        AddClassDesk("ClassDesk04", 260, 126);
        AddClassDesk("ClassDesk05", 84, 166);
        AddClassDesk("ClassDesk06", 144, 166);
        AddClassDesk("ClassDesk07", 203, 166);
        AddClassDesk("ClassDesk08", 260, 166);

        AddSolidRectangle("ClassroomFrontWall", 29, 207, 278, 18, 216);
    }

    private void BuildComputerLab()
    {
        AddSolidRectangle("ServerRacks", 479, 47, 50, 48, 72);
        AddPottedPlant("LabPlant", 713, 52, 91, 14, 28, 9, 20);

        AddLabDesk("LabDesk01", 530, 111);
        AddLabDesk("LabDesk02", 592, 111);
        AddLabDesk("LabDesk03", 654, 111);
        AddLabDesk("LabDesk04", 528, 160);
        AddLabDesk("LabDesk05", 592, 160);
        AddLabDesk("LabDesk06", 656, 160);
        AddLabDesk("LabDesk07", 528, 200);
        AddLabDesk("LabDesk08", 592, 200);
        AddLabDesk("LabDesk09", 656, 200);

        AddSolidRectangle("LabFrontWall", 459, 208, 280, 18, 217);
    }

    private void BuildProfessorRoom()
    {
        AddPottedPlant("ProfessorPlantLeft", 57, 239, 286, 15, 30, 9, 26);
        AddSolidRectangle("ProfessorShelves", 72, 238, 89, 49, 260);
        AddSolidRectangle("ProfessorCabinet", 203, 257, 58, 50, 280);
        AddProfessorDesk();
        AddCoffeeCounter();
        AddMeetingTable();
        AddPottedPlant("ProfessorPlantRight", 248, 383, 432, 15, 31, 9, 27);
        AddSolidRectangle("ProfessorRoomFrontWall", 29, 432, 242, 18, 440);
    }

    private void BuildExamRoom()
    {
        AddPottedPlant("ExamPlantLeft", 487, 248, 288, 15, 29, 9, 20);
        AddExamTerminal();
        AddPottedPlant("ExamPlantRight", 713, 248, 288, 15, 29, 9, 20);

        AddExamDesk("ExamDesk01", 523, 334);
        AddExamDesk("ExamDesk02", 584, 334);
        AddExamDesk("ExamDesk03", 640, 336);
        AddExamDesk("ExamDesk04", 696, 336);
        AddExamDesk("ExamDesk05", 523, 383);
        AddExamDesk("ExamDesk06", 584, 383);
        AddExamDesk("ExamDesk07", 640, 384);
        AddExamDesk("ExamDesk08", 696, 384);

        AddSolidRectangle("ExamRoomFrontWall", 459, 424, 280, 20, 432);
    }

    private void BuildEntrance()
    {
        AddPottedPlant("EntrancePlantLeft", 304, 435, 480, 19, 35, 12, 19);
        AddPottedPlant("EntrancePlantRight", 432, 434, 480, 19, 35, 12, 19);

        Node2D door = CreateGroup("EntranceDoorFrame", 471);
        AddRectanglePiece(door, "TransparentDoor", 337, 416, 62, 72);
    }

    private void AddClassDesk(string name, float x, float y)
    {
        Node2D desk = CreateGroup(name, y);
        AddRectanglePiece(desk, "TransparentSprite", x - 19, y - 24, 38, 43);
    }

    private void AddLabDesk(string name, float x, float y)
    {
        Node2D desk = CreateGroup(name, y);
        AddRectanglePiece(desk, "TransparentSprite", x - 32, y - 30, 64, 58);
    }

    private void AddExamDesk(string name, float x, float y)
    {
        Node2D desk = CreateGroup(name, y);
        AddRectanglePiece(desk, "TransparentSprite", x - 20, y - 31, 40, 54);
    }

    private void AddProfessorDesk()
    {
        Node2D desk = CreateGroup("ProfessorDesk", 337);
        AddRectanglePiece(desk, "TransparentSprite", 110, 288, 93, 72);
    }

    private void AddCoffeeCounter()
    {
        Node2D counter = CreateGroup("CoffeeCounter", 399);
        AddRectanglePiece(counter, "TransparentSprite", 38, 331, 76, 98);
    }

    private void AddMeetingTable()
    {
        Node2D table = CreateGroup("MeetingTable", 408);
        AddRectanglePiece(table, "TransparentSprite", 137, 371, 90, 82);
    }

    private void AddExamTerminal()
    {
        Node2D terminal = CreateGroup("ExamTerminal", 263);
        AddRectanglePiece(terminal, "TransparentSprite", 552, 232, 80, 79);
    }

    private void AddPottedPlant(
        string name,
        float centerX,
        float topY,
        float sortY,
        float foliageHalfWidth,
        float foliageHeight,
        float potHalfWidth,
        float potHeight)
    {
        Node2D plant = CreateGroup(name, sortY);
        AddRectanglePiece(
            plant,
            "TransparentSprite",
            centerX - foliageHalfWidth - 3,
            topY,
            (foliageHalfWidth + 3) * 2,
            foliageHeight + potHeight + 6);
    }

    private void AddSolidRectangle(
        string name,
        float x,
        float y,
        float width,
        float height,
        float sortY)
    {
        Node2D group = CreateGroup(name, sortY);
        AddRectanglePiece(group, "Body", x, y, width, height);
    }

    private Node2D CreateGroup(string name, float sortY)
    {
        Node2D group = new()
        {
            Name = name,
            Position = new Vector2(0, sortY)
        };

        AddChild(group);
        return group;
    }

    private void AddRectanglePiece(
        Node2D group,
        string name,
        float x,
        float y,
        float width,
        float height)
    {
        AddPolygonPiece(
            group,
            name,
            new Vector2(x, y),
            new Vector2(x + width, y),
            new Vector2(x + width, y + height),
            new Vector2(x, y + height));
    }

    private void AddPolygonPiece(Node2D group, string name, params Vector2[] sourcePoints)
    {
        Vector2[] localPoints = new Vector2[sourcePoints.Length];

        for (int index = 0; index < sourcePoints.Length; index++)
        {
            localPoints[index] = sourcePoints[index] - group.Position;
        }

        Polygon2D piece = new()
        {
            Name = name,
            Polygon = localPoints,
            UV = sourcePoints,
            Texture = SourceTexture
        };

        group.AddChild(piece);
    }
}

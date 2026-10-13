using System.Text;
using StorageHub.Core;
using Xunit;
using Xunit.Abstractions;
using static StorageHub.Tests.PackingTests;

namespace StorageHub.Tests;

/// <summary>
/// Three bags as the player saw them in game, laid out by the compact layout of the time. Each left single empty
/// cells around the small items that no big item could use.
/// </summary>
public class PlayerBagTests
{
    private static readonly PackOptions Compact = new();

    private readonly ITestOutputHelper _output;

    public PlayerBagTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Each_player_bag_as_found_is_a_valid_layout(int number)
    {
        var bag = PlayerBag(number);

        Assert.True(Packing.IsValid(bag.Space, bag.Items, bag.AsFound));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Each_player_bag_packs_validly_and_scores_at_least_as_well_as_it_was_found(int number)
    {
        var bag = PlayerBag(number);

        var packed = Fits(Packing.Pack(bag.Space, bag.Items, Compact));

        Assert.True(Packing.IsValid(bag.Space, bag.Items, packed));
        Assert.True(Score(bag, packed).CompareTo(Score(bag, bag.AsFound)) >= 0, $"packed {Score(bag, packed)} against found {Score(bag, bag.AsFound)}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void The_first_two_player_bags_pack_with_no_stranded_cells(int number)
    {
        var bag = PlayerBag(number);

        var packed = Fits(Packing.Pack(bag.Space, bag.Items, Compact));

        Assert.Equal(0, Score(bag, packed).StrandedCells);
    }

    [Fact]
    public void The_third_player_bags_four_free_cells_end_up_as_one_2_by_2_square()
    {
        // Four free cells with none stranded and a largest free rectangle of 4 can only be a 2 by 2 square.
        var bag = PlayerBag(3);

        var packed = Fits(Packing.Pack(bag.Space, bag.Items, Compact));
        var score = Score(bag, packed);

        Assert.Equal(4, Render(bag, packed).Count(cell => cell == '.'));
        Assert.Equal(0, score.StrandedCells);
        Assert.Equal(4, score.LargestFreeRectangle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void The_before_and_after_layouts_of_each_player_bag_are_shown_in_the_test_output(int number)
    {
        var bag = PlayerBag(number);

        var packed = Fits(Packing.Pack(bag.Space, bag.Items, Compact));

        _output.WriteLine($"Bag {number} as found, {Score(bag, bag.AsFound)}:");
        _output.WriteLine(Render(bag, bag.AsFound));
        _output.WriteLine($"Bag {number} packed, {Score(bag, packed)}:");
        _output.WriteLine(Render(bag, packed));

        Assert.True(Packing.IsValid(bag.Space, bag.Items, bag.AsFound));
        Assert.True(Packing.IsValid(bag.Space, bag.Items, packed));
    }

    /// <summary>Bags seen in a real game. x counts right and y down from the top-left; one letter is one kind of item.</summary>
    private static Bag PlayerBag(int number) =>
        number switch
        {
            1 => ParseBag(8, 10,
                "A 2x1 0,0; B 1x2 2,0 3,0; C 1x2 0,1; D 1x1 1,1; E 1x1 1,2; F 2x2 6,2; G 2x2 0,4; H 2x2 2,4; I 2x2 4,4 6,4; " +
                "J 2x2 1,6 3,6; K 2x2 1,8 3,8; L 3x4 5,6"),
            2 => ParseBag(12, 10,
                "M 1x2 0,0 1,0 2,0; N 1x2 3,0 4,0; O 2x1 5,0 7,0; P 1x2 5,1 6,1 7,1; Q 1x2 8,1; R 1x2 0,2 1,2 2,2 3,2; " +
                "S 1x1 4,2 4,3; T 1x1 5,3 6,3 7,3 8,3 0,4; U 1x1 1,4 2,4 3,4; V 1x1 4,4 5,4 6,4 7,4; W 1x1 8,4; X 1x1 0,5; " +
                "Y 1x1 1,5 2,5 3,5 4,5; Z1 1x1 5,5; Z2 1x1 6,5; Z3 1x1 7,5 8,5; a 2x2 10,5; b 2x2 0,6; c 1x1 2,6; d 1x1 3,6; " +
                "e 2x2 6,6 2,7; f 2x2 4,7; g 4x3 8,7; h 2x2 0,8; i 2x2 6,8; j 4x1 2,9"),
            3 => ParseBag(8, 10,
                "k 2x1 0,0 2,0 4,0 6,0 0,1; l 2x1 2,1; m 2x1 4,1; n 2x1 6,1 0,2 2,2 4,2; o 2x1 6,2 0,3 2,3; p 1x2 4,3 5,3; " +
                "q 1x1 6,3 7,3; r 1x1 0,4 1,4 2,4 3,4 6,4; s 1x1 7,4 0,5 1,5; t 1x1 2,5 3,5; u 2x2 0,6 2,6; v 2x2 4,6 6,6; " +
                "w 2x2 0,8; x 2x2 2,8; y 2x2 4,8 6,8"),
            _ => throw new ArgumentOutOfRangeException(nameof(number), number, "there are three player bags"),
        };

    /// <summary>
    /// Reads a bag from "kind WxH x,y x,y; ...". Items get ids in the order listed and each kind its own ConfigId, in the
    /// order the kinds first appear. A kind is drawn as the last character of its name.
    /// </summary>
    private static Bag ParseBag(int cols, int rows, string kinds)
    {
        var items = new List<PackItem>();
        var asFound = new Dictionary<long, Cell>();
        var labels = new Dictionary<long, char>();
        var entries = kinds.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var kind = 0; kind < entries.Length; kind++)
        {
            var parts = entries[kind].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var size = parts[1].Split('x').Select(int.Parse).ToArray();

            foreach (var at in parts.Skip(2).Select(ParseCell))
            {
                var item = new PackItem(items.Count + 1, size[0], size[1], kind + 1, 0);

                items.Add(item);

                asFound[item.Id] = at;
                labels[item.Id] = parts[0][^1];
            }
        }

        return new Bag(Space(cols, rows), items, asFound, labels);
    }

    private static Cell ParseCell(string text)
    {
        var xy = text.Split(',').Select(int.Parse).ToArray();

        return new Cell(xy[0], xy[1]);
    }

    /// <summary>
    /// The bag as rows of characters: '.' free, '#' mold, otherwise the label of the item on the cell. Any part of an
    /// item off the grid is left out, so a broken layout still prints.
    /// </summary>
    private static string Render(Bag bag, IReadOnlyDictionary<long, Cell> positions)
    {
        var grid = bag.Space.Grid;
        var cells = new char[grid.Cols, grid.Rows];

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++) cells[x, y] = bag.Space.Blocked.Contains(new Cell(x, y)) ? '#' : '.';
        }

        foreach (var item in bag.Items)
        {
            var at = positions[item.Id];

            for (var y = at.Y; y < at.Y + item.H; y++)
            {
                for (var x = at.X; x < at.X + item.W; x++)
                {
                    if (grid.Holds(new Cell(x, y), 1, 1)) cells[x, y] = bag.Labels[item.Id];
                }
            }
        }

        var text = new StringBuilder();

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++) text.Append(cells[x, y]);

            text.Append('\n');
        }

        return text.ToString();
    }

    private static LayoutScore Score(Bag bag, IReadOnlyDictionary<long, Cell> positions) => Packing.Score(bag.Space, bag.Items, positions, Compact);

    private sealed record Bag(BagSpace Space, IReadOnlyList<PackItem> Items, IReadOnlyDictionary<long, Cell> AsFound, IReadOnlyDictionary<long, char> Labels);
}

namespace Model;

public class ShowService
{
    public List<Universe> Universes { get; set; } = [];
    public List<Fixture> Fixtures { get; set; } = [];

    public Universe? GetUniverse(byte number) =>
        Universes.FirstOrDefault(u => u.Number == number);
}

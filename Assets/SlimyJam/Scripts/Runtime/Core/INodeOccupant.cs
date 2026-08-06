using SlimyJam.Data;

namespace SlimyJam.Core
{
    /// <summary>
    /// Gameplay'de collider yoktur; blocking yalnızca node sahipliği üzerinden değerlendirilir (GDD 9.1).
    /// </summary>
    public interface INodeOccupant
    {
        RopeColor Color { get; }
    }

    public interface IRopeOccupant : INodeOccupant
    {
        int RopeId { get; }
    }

    public interface IHoleOccupant : INodeOccupant
    {
        int HoleId { get; }
        int NodeId { get; }
    }
}

using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.Pathfinding;

namespace SlimyJam.Level
{
    /// <summary>
    /// Bir level'ın logical runtime servisleri. Yükleme sırası GDD 13.2 ile birebir:
    /// Build Node Lookup -> Connect Graph -> Register Holes -> Register Ropes -> Build Occupancy Map.
    /// </summary>
    public sealed class SlimyLevelContext
    {
        public GraphRepository Graph { get; private set; }
        public NodeOccupancyMap Occupancy { get; private set; }
        public PathfindingService Pathfinding { get; private set; }
        public FreeEndpointChooser FreeEndpointChooser { get; private set; }
        public StepPlanner StepPlanner { get; private set; }
        public CollectionService Collection { get; private set; }
        public SlimyConfig Config { get; private set; }

        /// <summary>Toplanmamış rope'lar. Boşaldığında level kazanılır (GDD 11.1).</summary>
        public List<RopeModel> ActiveRopes { get; private set; }

        public List<HoleModel> Holes { get; private set; }

        public static SlimyLevelContext Build(SlimyLevelData data, SlimyConfig config)
        {
            var context = new SlimyLevelContext
            {
                Config = config,
                Graph = new GraphRepository(data.groundNodes),
                Occupancy = new NodeOccupancyMap(),
                ActiveRopes = new List<RopeModel>(),
                Holes = new List<HoleModel>()
            };

            context.Pathfinding = new PathfindingService(context.Graph, context.Occupancy);
            context.FreeEndpointChooser = new FreeEndpointChooser(context.Graph, context.Occupancy);
            context.StepPlanner = new StepPlanner(context.Graph, context.Occupancy, context.FreeEndpointChooser);

            // Hole'lar rope'lardan önce occupy edilir; böylece rope kaydı çakışmayı erken yakalar.
            for (int i = 0; i < data.holes.Count; i++)
            {
                var holeData = data.holes[i];
                context.Holes.Add(new HoleModel(holeData.id, holeData.color, holeData.nodeId));
            }

            for (int i = 0; i < data.ropes.Count; i++)
            {
                var ropeData = data.ropes[i];
                context.ActiveRopes.Add(new RopeModel(ropeData.id, ropeData.color, ropeData.occupiedNodeIds));
            }

            context.Collection = new CollectionService(context.Graph, context.Occupancy, context.Holes,
                context.ActiveRopes);
            context.Collection.RegisterHoleOccupancy();

            for (int i = 0; i < context.ActiveRopes.Count; i++)
            {
                context.StepPlanner.RegisterInitialOccupancy(context.ActiveRopes[i]);
            }

            return context;
        }

        /// <summary>Her rope kendi resolver'ını kullanır; hysteresis ve path cache state'i izole kalır.</summary>
        public PointerTargetResolver CreateResolver()
        {
            return new PointerTargetResolver(Graph, Occupancy, Pathfinding, Config);
        }

        public RopeMovementController CreateMovementController(RopeModel rope)
        {
            return new RopeMovementController(rope, Graph, StepPlanner, CreateResolver(), Config);
        }
    }
}

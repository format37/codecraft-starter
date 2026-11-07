using System.Collections.Generic;
using System.Linq;
using Aicup2020.Model;

namespace Aicup2020
{
    public class MyStrategy
    {
        // One MyStrategy plays the whole game: anything you keep in its fields survives from tick to tick.
        DebugInterface debug;

        void Log(string text)
        {
            if (debug != null)
                debug.Send(new DebugCommand.Add(new DebugData.Log(text)));
        }

        void DrawRectangle(float x0, float y0, float x1, float y1) =>
            DrawRectangle(x0, y0, x1, y1, new Color(1, 0, 0, 1));

        void DrawRectangle(float x0, float y0, float x1, float y1, Color color)
        {
            if (debug == null)
                return;
            var corners = new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) };
            var vertices = new List<ColoredVertex>();
            for (int i = 0; i < 4; i++)  // Lines takes vertices in pairs: one pair per side
                foreach (var (x, y) in new[] { corners[i], corners[(i + 1) % 4] })
                    vertices.Add(new ColoredVertex(new Vec2Float(x, y), new Vec2Float(0, 0), color));
            debug.Send(new DebugCommand.Add(
                new DebugData.Primitives(vertices.ToArray(), PrimitiveType.Lines)));
        }

        public Action GetAction(PlayerView playerView, DebugInterface debugInterface)
        {
            debug = debugInterface;  // null on the server and in batch mode: there is no viewer to draw in
            int me = playerView.MyId;
            var houseProps = playerView.EntityProperties[EntityType.House];
            int resources = playerView.Players.First(p => p.Id == me).Resource;
            var mine = playerView.Entities.Where(e => e.PlayerId == me).ToList();
            var builders = mine.Where(e => e.EntityType == EntityType.BuilderUnit).ToList();
            var builderBase = mine.Where(e => e.EntityType == EntityType.BuilderBase)
                .Cast<Entity?>().FirstOrDefault();
            var house = mine.Where(e => e.EntityType == EntityType.House).Cast<Entity?>().FirstOrDefault();

            var count = new Dictionary<EntityType, int>();
            foreach (var e in mine)
                count[e.EntityType] = count.GetValueOrDefault(e.EntityType, 0) + 1;
            foreach (var (entityType, n) in count)
                Log($"{entityType}: {n}");
            if (house != null)
                Log($"house health: {house.Value.Health}/{houseProps.MaxHealth}");

            // One house, right of the builder base (which is 5x5)
            Vec2Int? houseSpot = null;
            if (house == null && builderBase != null && resources >= houseProps.InitialCost)
            {
                houseSpot = new Vec2Int(builderBase.Value.Position.X + 5, builderBase.Value.Position.Y + 4);
                DrawRectangle(houseSpot.Value.X, houseSpot.Value.Y,
                              houseSpot.Value.X + houseProps.Size, houseSpot.Value.Y + houseProps.Size);
            }

            // Of the actions given to an entity, the first one possible this tick is done, in the order
            // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
            // and starts the job as soon as it is next to it.
            var result = new Action(new Dictionary<int, EntityAction>());
            foreach (var builder in builders)
            {
                if (houseSpot != null)
                {
                    var build = new BuildAction(EntityType.House, houseSpot.Value);
                    result.EntityActions[builder.Id] = new EntityAction(
                        new MoveAction(houseSpot.Value, true, false), build, null, null);
                }
                else if (house != null && house.Value.Health < houseProps.MaxHealth)
                {
                    // A new house starts with little health and works only once repaired to full
                    result.EntityActions[builder.Id] = new EntityAction(
                        new MoveAction(house.Value.Position, true, false), null, null,
                        new RepairAction(house.Value.Id));
                }
                else
                {
                    // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                    var step = new Vec2Int(builder.Position.X + 1, builder.Position.Y);
                    result.EntityActions[builder.Id] = new EntityAction(
                        new MoveAction(step, true, false), null, null, null);
                }
            }
            return result;
        }

        public void DebugUpdate(PlayerView playerView, DebugInterface debugInterface)
        {
            // Runner.cs calls this while the viewer waits between ticks;
            // drawings sent here show on every tick
        }
    }
}

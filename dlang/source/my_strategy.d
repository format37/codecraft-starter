import model;
import debug_interface;
import std.algorithm;
import std.array;
import std.format;
import std.typecons;

class MyStrategy
{
    // One MyStrategy plays the whole game: anything you keep in its fields survives from tick to tick.
    DebugInterface debug_;

    void log(string text)
    {
        if (debug_)
            debug_.send(new DebugCommand.Add(new DebugData.Log(text)));
    }

    void drawRectangle(float x0, float y0, float x1, float y1, Color color = Color(1, 0, 0, 1))
    {
        if (!debug_)
            return;
        auto corners = [Vec2Float(x0, y0), Vec2Float(x1, y0), Vec2Float(x1, y1), Vec2Float(x0, y1)];
        ColoredVertex[] vertices;
        foreach (i; 0 .. 4)  // LINES takes vertices in pairs: one pair per side
            foreach (corner; [corners[i], corners[(i + 1) % 4]])
                vertices ~= ColoredVertex(nullable(corner), Vec2Float(0, 0), color);
        debug_.send(new DebugCommand.Add(new DebugData.Primitives(vertices, PrimitiveType.Lines)));
    }

    Action getAction(PlayerView playerView, DebugInterface debugInterface)
    {
        debug_ = debugInterface;  // null on the server and in batch mode: there is no viewer to draw in
        const me = playerView.myId;
        auto houseProps = playerView.entityProperties[EntityType.House];
        const resources = playerView.players.find!(p => p.id == me).front.resource;
        auto mine = playerView.entities.filter!(e => !e.playerId.isNull && e.playerId.get == me).array;
        auto builders = mine.filter!(e => e.entityType == EntityType.BuilderUnit).array;
        auto base = mine.find!(e => e.entityType == EntityType.BuilderBase);
        auto house = mine.find!(e => e.entityType == EntityType.House);

        int[EntityType] count;
        foreach (e; mine)
            count[e.entityType]++;
        foreach (entityType; count.keys.sort)
            log(format("%s: %s", entityType, count[entityType]));
        if (!house.empty)
            log(format("house health: %s/%s", house.front.health, houseProps.maxHealth));

        // One house, right of the builder base (which is 5x5)
        Nullable!Vec2Int houseSpot;
        if (house.empty && !base.empty && resources >= houseProps.initialCost)
        {
            houseSpot = Vec2Int(base.front.position.x + 5, base.front.position.y + 4);
            drawRectangle(houseSpot.get.x, houseSpot.get.y,
                          houseSpot.get.x + houseProps.size, houseSpot.get.y + houseProps.size);
        }

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        auto result = Action(null);
        foreach (builder; builders)
        {
            if (!houseSpot.isNull)
            {
                auto build = BuildAction(EntityType.House, houseSpot.get);
                result.entityActions[builder.id] = EntityAction(
                    nullable(MoveAction(houseSpot.get, true, false)), nullable(build),
                    Nullable!AttackAction.init, Nullable!RepairAction.init);
            }
            else if (!house.empty && house.front.health < houseProps.maxHealth)
            {
                // A new house starts with little health and works only once repaired to full
                result.entityActions[builder.id] = EntityAction(
                    nullable(MoveAction(house.front.position, true, false)), Nullable!BuildAction.init,
                    Nullable!AttackAction.init, nullable(RepairAction(house.front.id)));
            }
            else
            {
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                auto step = Vec2Int(builder.position.x + 1, builder.position.y);
                result.entityActions[builder.id] = EntityAction(
                    nullable(MoveAction(step, true, false)), Nullable!BuildAction.init,
                    Nullable!AttackAction.init, Nullable!RepairAction.init);
            }
        }
        return result;
    }

    void debugUpdate(PlayerView playerView, DebugInterface debugInterface)
    {
        // app.d calls this while the viewer waits between ticks; drawings sent here show on every tick
    }
}

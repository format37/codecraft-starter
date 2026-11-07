import model.*;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.stream.Collectors;

public class MyStrategy {
    // One MyStrategy plays the whole game: anything you keep in its fields survives from tick to tick.
    private DebugInterface debug;

    private void log(String text) {
        if (debug != null) {
            debug.send(new DebugCommand.Add(new DebugData.Log(text)));
        }
    }

    private void drawRectangle(int x0, int y0, int x1, int y1, Color color) {
        if (debug == null) {
            return;
        }
        int[][] corners = {{x0, y0}, {x1, y0}, {x1, y1}, {x0, y1}};
        List<ColoredVertex> vertices = new ArrayList<>();
        for (int i = 0; i < 4; i++) {  // LINES takes vertices in pairs: one pair per side
            for (int[] xy : new int[][]{corners[i], corners[(i + 1) % 4]}) {
                vertices.add(new ColoredVertex(new Vec2Float(xy[0], xy[1]), new Vec2Float(0, 0), color));
            }
        }
        debug.send(new DebugCommand.Add(
                new DebugData.Primitives(vertices.toArray(new ColoredVertex[0]), PrimitiveType.LINES)));
    }

    public Action getAction(PlayerView playerView, DebugInterface debugInterface) {
        debug = debugInterface;  // null on the server and in batch mode: there is no viewer to draw in
        int me = playerView.getMyId();
        EntityProperties houseProps = playerView.getEntityProperties().get(EntityType.HOUSE);
        int resources = Arrays.stream(playerView.getPlayers())
                .filter(p -> p.getId() == me).findFirst().get().getResource();
        List<Entity> mine = Arrays.stream(playerView.getEntities())
                .filter(e -> Objects.equals(e.getPlayerId(), me)).collect(Collectors.toList());
        List<Entity> builders = mine.stream()
                .filter(e -> e.getEntityType() == EntityType.BUILDER_UNIT).collect(Collectors.toList());
        Entity base = mine.stream()
                .filter(e -> e.getEntityType() == EntityType.BUILDER_BASE).findFirst().orElse(null);
        Entity house = mine.stream()
                .filter(e -> e.getEntityType() == EntityType.HOUSE).findFirst().orElse(null);

        Map<EntityType, Integer> count = new LinkedHashMap<>();
        for (Entity e : mine) {
            count.put(e.getEntityType(), count.getOrDefault(e.getEntityType(), 0) + 1);
        }
        count.forEach((entityType, n) -> log(entityType.name() + ": " + n));
        if (house != null) {
            log("house health: " + house.getHealth() + "/" + houseProps.getMaxHealth());
        }

        // One house, right of the builder base (which is 5x5)
        Vec2Int houseSpot = null;
        if (house == null && base != null && resources >= houseProps.getInitialCost()) {
            houseSpot = new Vec2Int(base.getPosition().getX() + 5, base.getPosition().getY() + 4);
            drawRectangle(houseSpot.getX(), houseSpot.getY(),
                          houseSpot.getX() + houseProps.getSize(), houseSpot.getY() + houseProps.getSize(),
                          new Color(1, 0, 0, 1));
        }

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        Action result = new Action(new HashMap<>());
        for (Entity builder : builders) {
            if (houseSpot != null) {
                BuildAction build = new BuildAction(EntityType.HOUSE, houseSpot);
                result.getEntityActions().put(builder.getId(), new EntityAction(
                        new MoveAction(houseSpot, true, false), build, null, null));
            } else if (house != null && house.getHealth() < houseProps.getMaxHealth()) {
                // A new house starts with little health and works only once repaired to full
                result.getEntityActions().put(builder.getId(), new EntityAction(
                        new MoveAction(house.getPosition(), true, false), null, null,
                        new RepairAction(house.getId())));
            } else {
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                Vec2Int step = new Vec2Int(builder.getPosition().getX() + 1, builder.getPosition().getY());
                result.getEntityActions().put(builder.getId(), new EntityAction(
                        new MoveAction(step, true, false), null, null, null));
            }
        }
        return result;
    }

    public void debugUpdate(PlayerView playerView, DebugInterface debugInterface) {
        // Runner.java calls this while the viewer waits between ticks; drawings sent here show on every tick
    }
}

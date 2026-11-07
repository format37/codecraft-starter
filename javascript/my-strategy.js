const {
    EntityType, Vec2Int, Vec2Float, Color, ColoredVertex, PrimitiveType, DebugData, DebugCommand,
    MoveAction, BuildAction, RepairAction, EntityAction, Action,
} = require('./model/index');

class MyStrategy {
    // One MyStrategy plays the whole game: anything you keep on this survives from tick to tick.

    async log(text) {
        if (this.debug) {
            await this.debug.send(new DebugCommand.Add(new DebugData.Log(text)));
        }
    }

    async drawRectangle(x0, y0, x1, y1, color = new Color(1, 0, 0, 1)) {
        if (!this.debug) {
            return;
        }
        const corners = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
        const vertices = [];
        for (let i = 0; i < 4; i++) {  // LINES takes vertices in pairs: one pair per side
            for (const [x, y] of [corners[i], corners[(i + 1) % 4]]) {
                vertices.push(new ColoredVertex(new Vec2Float(x, y), new Vec2Float(0, 0), color));
            }
        }
        await this.debug.send(new DebugCommand.Add(new DebugData.Primitives(vertices, PrimitiveType.Lines)));
    }

    async getAction(playerView, debugInterface) {
        this.debug = debugInterface;  // null on the server and in batch mode: there is no viewer to draw in
        const me = playerView.myId;
        const houseProps = playerView.entityProperties.get(EntityType.House);
        const resources = playerView.players.find(p => p.id === me).resource;
        const mine = playerView.entities.filter(e => e.playerId === me);
        const builders = mine.filter(e => e.entityType === EntityType.BuilderUnit);
        const base = mine.find(e => e.entityType === EntityType.BuilderBase);
        const house = mine.find(e => e.entityType === EntityType.House);

        const count = new Map();
        for (const e of mine) {
            count.set(e.entityType, (count.get(e.entityType) || 0) + 1);
        }
        for (const [entityType, n] of count) {
            await this.log(`${Object.keys(EntityType).find(k => EntityType[k] === entityType)}: ${n}`);
        }
        if (house !== undefined) {
            await this.log(`house health: ${house.health}/${houseProps.maxHealth}`);
        }

        // One house, right of the builder base (which is 5x5)
        let houseSpot = null;
        if (house === undefined && base !== undefined && resources >= houseProps.initialCost) {
            houseSpot = new Vec2Int(base.position.x + 5, base.position.y + 4);
            await this.drawRectangle(houseSpot.x, houseSpot.y,
                                     houseSpot.x + houseProps.size, houseSpot.y + houseProps.size);
        }

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        const result = new Action(new Map());
        for (const builder of builders) {
            if (houseSpot !== null) {
                const build = new BuildAction(EntityType.House, houseSpot);
                result.entityActions.set(builder.id, new EntityAction(
                    new MoveAction(houseSpot, true, false), build, null, null));
            } else if (house !== undefined && house.health < houseProps.maxHealth) {
                // A new house starts with little health and works only once repaired to full
                result.entityActions.set(builder.id, new EntityAction(
                    new MoveAction(house.position, true, false), null, null, new RepairAction(house.id)));
            } else {
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                const step = new Vec2Int(builder.position.x + 1, builder.position.y);
                result.entityActions.set(builder.id, new EntityAction(
                    new MoveAction(step, true, false), null, null, null));
            }
        }
        return result;
    }

    async debugUpdate(playerView, debugInterface) {
        // index.js calls this while the viewer waits between ticks; drawings sent here show on every tick
    }
}

module.exports.MyStrategy = MyStrategy;

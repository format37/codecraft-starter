import model.*

class MyStrategy {
    // One MyStrategy plays the whole game: anything you keep in its properties survives from tick to tick.
    private var debug: DebugInterface? = null

    private fun log(text: String) {
        debug?.send(DebugCommand.Add(DebugData.Log(text)))
    }

    private fun drawRectangle(x0: Int, y0: Int, x1: Int, y1: Int, color: Color = Color(1f, 0f, 0f, 1f)) {
        val debug = debug ?: return
        val corners = listOf(x0 to y0, x1 to y0, x1 to y1, x0 to y1)
        val vertices = mutableListOf<ColoredVertex>()
        for (i in 0 until 4) {  // LINES takes vertices in pairs: one pair per side
            for ((x, y) in listOf(corners[i], corners[(i + 1) % 4])) {
                vertices.add(ColoredVertex(Vec2Float(x.toFloat(), y.toFloat()), Vec2Float(0f, 0f), color))
            }
        }
        debug.send(DebugCommand.Add(DebugData.Primitives(vertices.toTypedArray(), PrimitiveType.LINES)))
    }

    fun getAction(playerView: PlayerView, debugInterface: DebugInterface?): Action {
        debug = debugInterface  // null on the server and in batch mode: there is no viewer to draw in
        val me = playerView.myId
        val houseProps = playerView.entityProperties.getValue(EntityType.HOUSE)
        val resources = playerView.players.first { it.id == me }.resource
        val mine = playerView.entities.filter { it.playerId == me }
        val builders = mine.filter { it.entityType == EntityType.BUILDER_UNIT }
        val base = mine.firstOrNull { it.entityType == EntityType.BUILDER_BASE }
        val house = mine.firstOrNull { it.entityType == EntityType.HOUSE }

        val count = mutableMapOf<EntityType, Int>()
        for (e in mine) {
            count[e.entityType] = (count[e.entityType] ?: 0) + 1
        }
        for ((entityType, n) in count) {
            log("${entityType.name}: $n")
        }
        if (house != null) {
            log("house health: ${house.health}/${houseProps.maxHealth}")
        }

        // One house, right of the builder base (which is 5x5)
        var houseSpot: Vec2Int? = null
        if (house == null && base != null && resources >= houseProps.initialCost) {
            houseSpot = Vec2Int(base.position.x + 5, base.position.y + 4)
            drawRectangle(houseSpot.x, houseSpot.y,
                          houseSpot.x + houseProps.size, houseSpot.y + houseProps.size)
        }

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        val result = Action(mutableMapOf())
        for (builder in builders) {
            if (houseSpot != null) {
                val build = BuildAction(EntityType.HOUSE, houseSpot)
                result.entityActions[builder.id] = EntityAction(
                    MoveAction(houseSpot, true, false), build, null, null)
            } else if (house != null && house.health < houseProps.maxHealth) {
                // A new house starts with little health and works only once repaired to full
                result.entityActions[builder.id] = EntityAction(
                    MoveAction(house.position, true, false), null, null, RepairAction(house.id))
            } else {
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                val step = Vec2Int(builder.position.x + 1, builder.position.y)
                result.entityActions[builder.id] = EntityAction(
                    MoveAction(step, true, false), null, null, null)
            }
        }
        return result
    }

    fun debugUpdate(playerView: PlayerView, debugInterface: DebugInterface) {
        // Runner.kt calls this while the viewer waits between ticks; drawings sent here show on every tick
    }
}

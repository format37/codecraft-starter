require_relative 'model'

class MyStrategy
  # One MyStrategy plays the whole game: anything you keep in instance variables survives from tick to tick.

  def log(text)
    if @debug
      @debug.send(DebugCommand::Add.new(DebugData::Log.new(text)))
    end
  end

  def draw_rectangle(x0, y0, x1, y1, color = Color.new(1, 0, 0, 1))
    return unless @debug
    corners = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]
    vertices = []
    4.times do |i|  # LINES takes vertices in pairs: one pair per side
      [corners[i], corners[(i + 1) % 4]].each do |x, y|
        vertices << ColoredVertex.new(Vec2Float.new(x, y), Vec2Float.new(0, 0), color)
      end
    end
    @debug.send(DebugCommand::Add.new(DebugData::Primitives.new(vertices, PrimitiveType::LINES)))
  end

  def get_action(player_view, debug_interface)
    @debug = debug_interface  # nil on the server and in batch mode: there is no viewer to draw in
    me = player_view.my_id
    house_props = player_view.entity_properties[EntityType::HOUSE]
    resources = player_view.players.find { |p| p.id == me }.resource
    mine = player_view.entities.select { |e| e.player_id == me }
    builders = mine.select { |e| e.entity_type == EntityType::BUILDER_UNIT }
    base = mine.find { |e| e.entity_type == EntityType::BUILDER_BASE }
    house = mine.find { |e| e.entity_type == EntityType::HOUSE }

    count = {}
    mine.each do |e|
      count[e.entity_type] = count.fetch(e.entity_type, 0) + 1
    end
    count.each do |entity_type, n|
      log("#{EntityType.constants.find { |c| EntityType.const_get(c) == entity_type }}: #{n}")
    end
    if house
      log("house health: #{house.health}/#{house_props.max_health}")
    end

    # One house, right of the builder base (which is 5x5)
    house_spot = nil
    if house.nil? && base && resources >= house_props.initial_cost
      house_spot = Vec2Int.new(base.position.x + 5, base.position.y + 4)
      draw_rectangle(house_spot.x, house_spot.y,
                     house_spot.x + house_props.size, house_spot.y + house_props.size)
    end

    # Of the actions given to an entity, the first one possible this tick is done, in the order
    # attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
    # and starts the job as soon as it is next to it.
    result = Action.new({})
    builders.each do |builder|
      if house_spot
        build = BuildAction.new(EntityType::HOUSE, house_spot)
        result.entity_actions[builder.id] = EntityAction.new(
          MoveAction.new(house_spot, true, false), build, nil, nil)
      elsif house && house.health < house_props.max_health
        # A new house starts with little health and works only once repaired to full
        result.entity_actions[builder.id] = EntityAction.new(
          MoveAction.new(house.position, true, false), nil, nil, RepairAction.new(house.id))
      else
        # Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
        step = Vec2Int.new(builder.position.x + 1, builder.position.y)
        result.entity_actions[builder.id] = EntityAction.new(MoveAction.new(step, true, false), nil, nil, nil)
      end
    end
    result
  end

  def debug_update(player_view, debug_interface)
    # main.rb calls this while the viewer waits between ticks; drawings sent here show on every tick
  end
end

from model import *

# A starter strategy for CodeCraft (Russian AI Cup 2020): builders gather resources, repair the base
# and put up houses (and the bases that Round 2 starts without) as the population fills up; the bases
# train units, builders first; the army gathers at home until it is big enough, then hunts the nearest
# enemy, shooting whatever comes into range. Rules: https://github.com/format37/codecraft/blob/master/docs/doc-en.md.
# Make it yours.

BASES_WANTED = [EntityType.BUILDER_BASE, EntityType.RANGED_BASE]
FIRST_BUILDERS = 5        # the builder base spends first until it has this many; then the fighting bases do
MAX_BUILDERS = 12
ARMY_SIZE = 8             # fighting units wait at home until there are this many of them
REPAIR_RANGE = 6          # a builder this close to a damaged or unfinished building goes to repair it


class MyStrategy:
    def get_action(self, player_view, debug_interface):
        me = player_view.my_id
        size = player_view.map_size
        props = player_view.entity_properties
        mine = [e for e in player_view.entities if e.player_id == me]
        enemies = [e for e in player_view.entities if e.player_id is not None and e.player_id != me]
        resources = next(p.resource for p in player_view.players if p.id == me)

        count = {}
        for e in mine:
            count[e.entity_type] = count.get(e.entity_type, 0) + 1
        # population: active buildings provide it, every unit uses it (the engine refuses a unit beyond it)
        provided = sum(props[e.entity_type].population_provide for e in mine if e.active)
        used = sum(props[e.entity_type].population_use for e in mine)

        occupied = set()
        for e in player_view.entities:
            s = props[e.entity_type].size
            for dx in range(s):
                for dy in range(s):
                    occupied.add((e.position.x + dx, e.position.y + dy))

        def free(x, y, s):
            """Can an entity of size `s` be placed with its lower-left corner at (x, y)?"""
            return all(0 <= x + dx < size and 0 <= y + dy < size and (x + dx, y + dy) not in occupied
                       for dx in range(s) for dy in range(s))

        def spot_next_to(entity, s):
            """A free place for a new entity of size `s` touching `entity` (right, top, left, bottom)."""
            x, y, es = entity.position.x, entity.position.y, props[entity.entity_type].size
            for k in range(es):
                for cx, cy in ((x + es, y + k), (x + k, y + es), (x - s, y + k), (x + k, y - s)):
                    if free(cx, cy, s):
                        return Vec2Int(cx, cy)
            return None

        def distance(a, b):
            return abs(a.x - b.x) + abs(a.y - b.y)

        def nearest(entities, position):
            return min(entities, key=lambda e: distance(e.position, position), default=None)

        # what the builders should put up next: a missing base, then a house when the population is nearly full
        wanted = [t for t in BASES_WANTED if not count.get(t)]
        house_pending = any(e.entity_type == EntityType.HOUSE and not e.active for e in mine)
        if used + 3 > provided and not house_pending:
            wanted.append(EntityType.HOUSE)
        damaged = [e for e in mine if not props[e.entity_type].can_move and e.health < props[e.entity_type].max_health]
        fighters = [e for e in mine if props[e.entity_type].can_move and e.entity_type != EntityType.BUILDER_UNIT]
        home = next((e.position for e in mine if not props[e.entity_type].can_move), Vec2Int(size // 2, size // 2))

        # who spends first: the builder base while builders are few, the fighting bases (and houses) after that
        builder_bases = [e for e in mine if e.entity_type == EntityType.BUILDER_BASE]
        others = [e for e in mine if e.entity_type != EntityType.BUILDER_BASE]
        order = builder_bases + others if count.get(EntityType.BUILDER_UNIT, 0) < FIRST_BUILDERS else others + builder_bases

        result = Action({})
        for e in order:
            p = props[e.entity_type]
            move = build = attack = repair = None
            if e.entity_type == EntityType.BUILDER_UNIT:
                target = nearest(damaged, e.position)
                if target is not None and distance(target.position, e.position) <= REPAIR_RANGE:
                    move = MoveAction(target.position, True, False)     # a repair needs an adjacent builder
                    repair = RepairAction(target.id)
                else:
                    for t in wanted:
                        spot = spot_next_to(e, props[t].size)
                        if spot is not None and resources >= props[t].initial_cost:
                            build = BuildAction(t, spot)
                            resources -= props[t].initial_cost
                            wanted.remove(t)
                            break
                    if build is None:                                  # gather: attack the resources in sight
                        attack = AttackAction(None, AutoAttack(p.sight_range, [EntityType.RESOURCE]))
            elif p.can_move:                                           # a fighting unit
                enemy = nearest(enemies, e.position)
                if len(fighters) >= ARMY_SIZE and enemy is not None:   # the army is big enough: hunt
                    move = MoveAction(enemy.position, True, True)
                else:                                                  # otherwise guard the base
                    move = MoveAction(home, True, False)
                attack = AttackAction(None, AutoAttack(p.sight_range, []))
            elif p.build is not None and e.active:                     # a base: train its unit
                unit = p.build.options[0]
                cost = props[unit].initial_cost + count.get(unit, 0)   # a unit costs more the more you have
                spot = spot_next_to(e, 1)
                enough = unit == EntityType.BUILDER_UNIT and count.get(unit, 0) >= MAX_BUILDERS
                if spot is not None and not enough and resources >= cost and used + props[unit].population_use <= provided:
                    build = BuildAction(unit, spot)
                    resources -= cost
                    used += props[unit].population_use
                    count[unit] = count.get(unit, 0) + 1
            else:                                                      # a turret shoots; houses and walls just stand
                attack = AttackAction(None, AutoAttack(p.sight_range, []))
            result.entity_actions[e.id] = EntityAction(move, build, attack, repair)
        return result

    def debug_update(self, player_view, debug_interface):
        debug_interface.send(DebugCommand.Clear())
        debug_interface.get_state()

#include "MyStrategy.hpp"

#include <algorithm>
#include <format>
#include <map>
#include <memory>
#include <optional>
#include <vector>

MyStrategy::MyStrategy() {}

void MyStrategy::log(const std::string& text)
{
    if (debug) {
        debug->send(DebugCommand::Add(std::make_shared<DebugData::Log>(text)));
    }
}

void MyStrategy::drawRectangle(float x0, float y0, float x1, float y1, Color color)
{
    if (!debug) {
        return;
    }
    const Vec2Float corners[] = {Vec2Float(x0, y0), Vec2Float(x1, y0), Vec2Float(x1, y1), Vec2Float(x0, y1)};
    std::vector<ColoredVertex> vertices;
    for (int i = 0; i < 4; i++) {   // LINES takes vertices in pairs: one pair per side
        for (const Vec2Float& corner : {corners[i], corners[(i + 1) % 4]}) {
            vertices.push_back(ColoredVertex(std::make_shared<Vec2Float>(corner), Vec2Float(0, 0), color));
        }
    }
    debug->send(DebugCommand::Add(std::make_shared<DebugData::Primitives>(vertices, PrimitiveType::LINES)));
}

Action MyStrategy::getAction(const PlayerView& playerView, DebugInterface* debugInterface)
{
    debug = debugInterface;   // nullptr on the server and in batch mode: there is no viewer to draw in
    const int me = playerView.myId;
    const EntityProperties& houseProps = playerView.entityProperties.at(EntityType::HOUSE);
    const int resources =
        std::ranges::find_if(playerView.players, [&](const Player& p) { return p.id == me; })->resource;
    std::vector<const Entity*> mine;
    for (const Entity& e : playerView.entities) {
        if (e.playerId && *e.playerId == me) {
            mine.push_back(&e);
        }
    }
    std::vector<const Entity*> builders;
    const Entity* base = nullptr;
    const Entity* house = nullptr;
    for (const Entity* e : mine) {
        if (e->entityType == EntityType::BUILDER_UNIT) {
            builders.push_back(e);
        }
        if (e->entityType == EntityType::BUILDER_BASE && base == nullptr) {
            base = e;
        }
        if (e->entityType == EntityType::HOUSE && house == nullptr) {
            house = e;
        }
    }

    std::map<EntityType, int> count;
    for (const Entity* e : mine) {
        count[e->entityType]++;
    }
    const char* const names[] = {"WALL", "HOUSE", "BUILDER_BASE", "BUILDER_UNIT", "MELEE_BASE",
                                 "MELEE_UNIT", "RANGED_BASE", "RANGED_UNIT", "RESOURCE", "TURRET"};
    for (const auto& [entityType, n] : count) {
        log(std::format("{}: {}", names[entityType], n));
    }
    if (house != nullptr) {
        log(std::format("house health: {}/{}", house->health, houseProps.maxHealth));
    }

    // One house, right of the builder base (which is 5x5)
    std::optional<Vec2Int> houseSpot;
    if (house == nullptr && base != nullptr && resources >= houseProps.initialCost) {
        houseSpot = Vec2Int(base->position.x + 5, base->position.y + 4);
        drawRectangle(houseSpot->x, houseSpot->y,
                      houseSpot->x + houseProps.size, houseSpot->y + houseProps.size);
    }

    // Of the actions given to an entity, the first one possible this tick is done, in the order
    // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
    // and starts the job as soon as it is next to it.
    Action result;
    for (const Entity* builder : builders) {
        if (houseSpot) {
            const auto build = std::make_shared<BuildAction>(EntityType::HOUSE, *houseSpot);
            result.entityActions[builder->id] = EntityAction(
                std::make_shared<MoveAction>(*houseSpot, true, false), build, nullptr, nullptr);
        } else if (house != nullptr && house->health < houseProps.maxHealth) {
            // A new house starts with little health and works only once repaired to full
            result.entityActions[builder->id] = EntityAction(
                std::make_shared<MoveAction>(house->position, true, false), nullptr, nullptr,
                std::make_shared<RepairAction>(house->id));
        } else {
            // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
            const Vec2Int step(builder->position.x + 1, builder->position.y);
            result.entityActions[builder->id] = EntityAction(
                std::make_shared<MoveAction>(step, true, false), nullptr, nullptr, nullptr);
        }
    }
    return result;
}

void MyStrategy::debugUpdate(const PlayerView& playerView, DebugInterface& debugInterface)
{
    // main.cpp calls this while the viewer waits between ticks; drawings sent here show on every tick
}

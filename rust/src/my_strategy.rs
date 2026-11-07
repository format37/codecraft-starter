use super::DebugInterface;
use model::*;
use std::collections::HashMap;

pub struct MyStrategy {
    // One MyStrategy plays the whole game: anything you keep on self survives from tick to tick.
}

impl MyStrategy {
    pub fn new() -> Self {
        Self {}
    }

    fn log(&self, debug: &mut Option<&mut DebugInterface>, text: String) {
        if let Some(debug) = debug {
            debug.send(DebugCommand::Add {
                data: DebugData::Log { text },
            });
        }
    }

    fn draw_rectangle(
        &self,
        debug: &mut Option<&mut DebugInterface>,
        x0: i32,
        y0: i32,
        x1: i32,
        y1: i32,
        color: Color,
    ) {
        let Some(debug) = debug else {
            return;
        };
        let corners = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
        let mut vertices = Vec::new();
        for i in 0..4 { // LINES takes vertices in pairs: one pair per side
            for (x, y) in [corners[i], corners[(i + 1) % 4]] {
                vertices.push(ColoredVertex {
                    world_pos: Some(Vec2F32 { x: x as f32, y: y as f32 }),
                    screen_offset: Vec2F32 { x: 0.0, y: 0.0 },
                    color: color.clone(),
                });
            }
        }
        debug.send(DebugCommand::Add {
            data: DebugData::Primitives {
                vertices,
                primitive_type: PrimitiveType::Lines,
            },
        });
    }

    pub fn get_action(
        &mut self,
        player_view: &PlayerView,
        debug_interface: Option<&mut DebugInterface>,
    ) -> Action {
        // None on the server and in batch mode: there is no viewer to draw in
        let mut debug = debug_interface;
        let me = player_view.my_id;
        let house_props = &player_view.entity_properties[&EntityType::House];
        let resources = player_view.players.iter().find(|p| p.id == me).unwrap().resource;
        let mine: Vec<&Entity> = player_view
            .entities
            .iter()
            .filter(|e| e.player_id == Some(me))
            .collect();
        let builders: Vec<&Entity> = mine
            .iter()
            .copied()
            .filter(|e| e.entity_type == EntityType::BuilderUnit)
            .collect();
        let base = mine.iter().copied().find(|e| e.entity_type == EntityType::BuilderBase);
        let house = mine.iter().copied().find(|e| e.entity_type == EntityType::House);

        let mut count: HashMap<EntityType, i32> = HashMap::new();
        for e in &mine {
            *count.entry(e.entity_type.clone()).or_insert(0) += 1;
        }
        let mut count: Vec<(EntityType, i32)> = count.into_iter().collect();
        count.sort_by_key(|(entity_type, _)| entity_type.clone() as i32);
        for (entity_type, n) in &count {
            self.log(&mut debug, format!("{:?}: {}", entity_type, n));
        }
        if let Some(house) = house {
            self.log(
                &mut debug,
                format!("house health: {}/{}", house.health, house_props.max_health),
            );
        }

        // One house, right of the builder base (which is 5x5)
        let mut house_spot = None;
        if let (None, Some(base)) = (house, base) {
            if resources >= house_props.initial_cost {
                let spot = Vec2I32 {
                    x: base.position.x + 5,
                    y: base.position.y + 4,
                };
                self.draw_rectangle(
                    &mut debug,
                    spot.x,
                    spot.y,
                    spot.x + house_props.size,
                    spot.y + house_props.size,
                    Color { r: 1.0, g: 0.0, b: 0.0, a: 1.0 },
                );
                house_spot = Some(spot);
            }
        }

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        let mut result = Action {
            entity_actions: HashMap::new(),
        };
        for builder in &builders {
            if let Some(house_spot) = &house_spot {
                let build = BuildAction {
                    entity_type: EntityType::House,
                    position: house_spot.clone(),
                };
                result.entity_actions.insert(
                    builder.id,
                    EntityAction {
                        move_action: Some(MoveAction {
                            target: house_spot.clone(),
                            find_closest_position: true,
                            break_through: false,
                        }),
                        build_action: Some(build),
                        attack_action: None,
                        repair_action: None,
                    },
                );
            } else if let Some(house) = house.filter(|house| house.health < house_props.max_health) {
                // A new house starts with little health and works only once repaired to full
                result.entity_actions.insert(
                    builder.id,
                    EntityAction {
                        move_action: Some(MoveAction {
                            target: house.position.clone(),
                            find_closest_position: true,
                            break_through: false,
                        }),
                        build_action: None,
                        attack_action: None,
                        repair_action: Some(RepairAction { target: house.id }),
                    },
                );
            } else {
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                let step = Vec2I32 {
                    x: builder.position.x + 1,
                    y: builder.position.y,
                };
                result.entity_actions.insert(
                    builder.id,
                    EntityAction {
                        move_action: Some(MoveAction {
                            target: step,
                            find_closest_position: true,
                            break_through: false,
                        }),
                        build_action: None,
                        attack_action: None,
                        repair_action: None,
                    },
                );
            }
        }
        result
    }

    pub fn debug_update(&mut self, _player_view: &PlayerView, _debug_interface: &mut DebugInterface) {
        // main.rs calls this while the viewer waits between ticks; drawings sent here show on every tick
    }
}

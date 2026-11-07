package main

import (
	. "aicup2020/model"
	"fmt"
)

type MyStrategy struct {
	// One MyStrategy plays the whole game: anything you keep in its fields survives from tick to tick.
	debug *DebugInterface
}

func NewMyStrategy() MyStrategy {
	return MyStrategy{}
}

func (strategy *MyStrategy) log(text string) {
	if strategy.debug != nil {
		strategy.debug.Send(DebugCommandAdd{Data: DebugDataLog{Text: text}})
	}
}

func (strategy *MyStrategy) drawRectangle(x0, y0, x1, y1 int32, color Color) {
	if strategy.debug == nil {
		return
	}
	corners := [4][2]int32{{x0, y0}, {x1, y0}, {x1, y1}, {x0, y1}}
	var vertices []ColoredVertex
	for i := 0; i < 4; i++ { // LINES takes vertices in pairs: one pair per side
		for _, corner := range [2][2]int32{corners[i], corners[(i+1)%4]} {
			vertices = append(vertices, ColoredVertex{
				WorldPos:     &Vec2Float32{X: float32(corner[0]), Y: float32(corner[1])},
				ScreenOffset: Vec2Float32{X: 0, Y: 0},
				Color:        color,
			})
		}
	}
	strategy.debug.Send(DebugCommandAdd{
		Data: DebugDataPrimitives{Vertices: vertices, PrimitiveType: PrimitiveTypeLines},
	})
}

func (strategy *MyStrategy) getAction(playerView PlayerView, debugInterface *DebugInterface) Action {
	strategy.debug = debugInterface // nil on the server and in batch mode: there is no viewer to draw in
	me := playerView.MyId
	houseProps := playerView.EntityProperties[EntityTypeHouse]
	var resources int32
	for _, p := range playerView.Players {
		if p.Id == me {
			resources = p.Resource
			break
		}
	}
	var mine []Entity
	for _, e := range playerView.Entities {
		if e.PlayerId != nil && *e.PlayerId == me {
			mine = append(mine, e)
		}
	}
	var builders []Entity
	var base, house *Entity
	for i, e := range mine {
		if e.EntityType == EntityTypeBuilderUnit {
			builders = append(builders, e)
		}
		if e.EntityType == EntityTypeBuilderBase && base == nil {
			base = &mine[i]
		}
		if e.EntityType == EntityTypeHouse && house == nil {
			house = &mine[i]
		}
	}

	count := map[EntityType]int{}
	for _, e := range mine {
		count[e.EntityType]++
	}
	names := []string{"Wall", "House", "BuilderBase", "BuilderUnit", "MeleeBase",
		"MeleeUnit", "RangedBase", "RangedUnit", "Resource", "Turret"}
	for entityType, name := range names {
		if n, ok := count[EntityType(entityType)]; ok {
			strategy.log(fmt.Sprintf("%s: %d", name, n))
		}
	}
	if house != nil {
		strategy.log(fmt.Sprintf("house health: %d/%d", house.Health, houseProps.MaxHealth))
	}

	// One house, right of the builder base (which is 5x5)
	var houseSpot *Vec2Int32
	if house == nil && base != nil && resources >= houseProps.InitialCost {
		houseSpot = &Vec2Int32{X: base.Position.X + 5, Y: base.Position.Y + 4}
		strategy.drawRectangle(houseSpot.X, houseSpot.Y,
			houseSpot.X+houseProps.Size, houseSpot.Y+houseProps.Size, Color{R: 1, G: 0, B: 0, A: 1})
	}

	// Of the actions given to an entity, the first one possible this tick is done, in the order
	// attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
	// and starts the job as soon as it is next to it.
	result := Action{EntityActions: map[int32]EntityAction{}}
	for _, builder := range builders {
		if houseSpot != nil {
			build := &BuildAction{EntityType: EntityTypeHouse, Position: *houseSpot}
			result.EntityActions[builder.Id] = EntityAction{
				MoveAction:  &MoveAction{Target: *houseSpot, FindClosestPosition: true, BreakThrough: false},
				BuildAction: build,
			}
		} else if house != nil && house.Health < houseProps.MaxHealth {
			// A new house starts with little health and works only once repaired to full
			result.EntityActions[builder.Id] = EntityAction{
				MoveAction:   &MoveAction{Target: house.Position, FindClosestPosition: true, BreakThrough: false},
				RepairAction: &RepairAction{Target: house.Id},
			}
		} else {
			// Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
			step := Vec2Int32{X: builder.Position.X + 1, Y: builder.Position.Y}
			result.EntityActions[builder.Id] = EntityAction{
				MoveAction: &MoveAction{Target: step, FindClosestPosition: true, BreakThrough: false},
			}
		}
	}
	return result
}

func (strategy *MyStrategy) debugUpdate(playerView PlayerView, debugInterface DebugInterface) {
	// main.go calls this while the viewer waits between ticks; drawings sent here show on every tick
}

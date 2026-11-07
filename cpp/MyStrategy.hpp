#ifndef _MY_STRATEGY_HPP_
#define _MY_STRATEGY_HPP_

#include "DebugInterface.hpp"
#include "model/Model.hpp"
#include <string>

class MyStrategy {
    // One MyStrategy plays the whole game: anything you keep in its members survives from tick to tick.
public:
    MyStrategy();
    void log(const std::string& text);
    void drawRectangle(float x0, float y0, float x1, float y1, Color color = Color(1, 0, 0, 1));
    Action getAction(const PlayerView& playerView, DebugInterface* debugInterface);
    void debugUpdate(const PlayerView& playerView, DebugInterface& debugInterface);

private:
    DebugInterface* debug = nullptr;
};

#endif
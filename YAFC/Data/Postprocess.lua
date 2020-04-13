-- This file is run after all mods are loaded
-- In case of breaking prototype changes, if YAFC is not updated, you may put data patching code here

-- Also some context info for YAFC

data["Item types"] = {"item", "ammo", "capsule", "gun", "item-with-entity-data", "item-with-label", "item-with-inventory",
            "blueprint-book", "item-with-tags", "selection-tool", "blueprint", "copy-paste-tool", "deconstruction-item",
            "upgrade-item", "module", "rail-planner", "tool", "armor", "mining-tool", "repair-tool"}
			
data["Entity types"] = {"accumulator", "artillery-turret", "beacon", "boiler", "character", "arithmetic-combinator", "decider-combinator", "constant-combinator", "container",
            "logistic-container", "infinity-container", "assembling-machine", "rocket-silo", "furnace", "electric-energy-interface", "electric-pole", "unit-spawner", "fish",
            "combat-robot", "construction-robot", "logistic-robot", "gate", "generator", "heat-interface", "heat-pipe", "inserter", "lab", "lamp", "land-mine", "market",
            "mining-drill", "offshore-pump", "pipe", "infinity-pipe", "pipe-to-ground", "player-port", "power-switch", "programmable-speaker", "pump", "radar", "curved-rail",
            "straight-rail", "rail-chain-signal", "rail-signal", "reactor", "roboport", "simple-entity", "simple-entity-with-owner", "simple-entity-with-force", "solar-panel",
            "storage-tank", "train-stop", "loader", "loader-1x1", "splitter", "transport-belt", "underground-belt", "tree", "turret", "ammo-turret", "electric-turret", "fluid-turret", "unit",
            "car", "artillery-wagon", "cargo-wagon", "fluid-wagon", "locomotive", "wall", "resource"};
			

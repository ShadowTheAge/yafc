data = {
  raw = {
    item = {
      wood = {
        type = "item",
        name = "wood",
        fuel_category = "chemical",
        fuel_value = "2MJ",
        burnt_result = "ash"
      },
      coal = {
        type = "item",
        name = "coal",
        fuel_category = "chemical",
        fuel_value = "4MJ",
        burnt_result = "ash"
      },
      fuel = {
        type = "item",
        name = "fuel",
        fuel_category = "chemical",
        fuel_value = "15MJ",
      },
      ash = {
        type = "item",
        name = "ash",
      },
      dummy_1 = {
        type = "item",
        name = "dummy_1",
      },
      dummy_2 = {
        type = "item",
        name = "dummy_2",
      },
      dummy_3 = {
        type = "item",
        name = "dummy_3",
      }
    },
    ["assembling-machine"] = {
      ["assembling-machine-f1"] = {
        type = "assembling-machine",
        name = "assembling-machine-f1",
        crafting_speed = 0.5,
        energy_source = {
          type = "burner",
          fuel_category = "chemical",
        },
        energy_usage = "75kW",
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
      ["assembling-machine-f2"] = {
        type = "assembling-machine",
        name = "assembling-machine-f2",
        crafting_speed = 0.75,
        energy_source = {
          type = "burner",
          fuel_category = "chemical",
        },
        energy_usage = "150kW",
        module_specification = {
          module_slots = 1,
        },
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
      ["assembling-machine-f3"] = {
        type = "assembling-machine",
        name = "assembling-machine-f3",
        crafting_speed = 1.25,
        energy_source = {
          type = "burner",
          fuel_category = "chemical",
        },
        energy_usage = "250kW",
        module_specification = {
          module_slots = 2,
        },
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
      ["assembling-machine-e1"] = {
        type = "assembling-machine",
        name = "assembling-machine-e1",
        crafting_speed = 0.75,
        energy_source = {
          type = "electric",
        },
        energy_usage = "150kW",
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
      ["assembling-machine-e2"] = {
        type = "assembling-machine",
        name = "assembling-machine-e2",
        crafting_speed = 1,
        energy_source = {
          type = "electric",
        },
        energy_usage = "250kW",
        module_specification = {
          module_slots = 2,
        },
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
      ["assembling-machine-e3"] = {
        type = "assembling-machine",
        name = "assembling-machine-e3",
        crafting_speed = 1.75,
        energy_source = {
          type = "electric",
        },
        energy_usage = "375kW",
        module_specification = {
          module_slots = 4,
        },
        crafting_categories = { "crafting" },
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
          "productivity",
        },
      },
    },
    beacon = {
      beacon = {
        type = "beacon",
        name = "beacon",
        allowed_effects = {
          "consumption",
          "speed",
          "pollution",
        },
        energy_source = {
          type = "electric",
        },
        energy_usage = "480kW",
        distribution_effectivity = 0.5,
        module_specification = {
          module_slots = 2,
        },
      },
    },
    module = {
      ["speed-module"] = {
        type = "module",
        name = "speed-module",
        effect = {
          speed = {
            bonus = 0.2,
          },
          consumption = {
            bonus = 0.5,
          },
        },
      },
      ["speed-module-2"] = {
        type = "module",
        name = "speed-module-2",
        effect = {
          speed = {
            bonus = 0.3,
          },
          consumption = {
            bonus = 0.6,
          },
        },
      },
      ["speed-module-3"] = {
        type = "module",
        name = "speed-module-3",
        effect = {
          speed = {
            bonus = 0.5,
          },
          consumption = {
            bonus = 0.7,
          },
        },
      },
      ["effectivity-module"] = {
        type = "module",
        name = "effectivity-module",
        effect = {
          consumption = {
            bonus = -0.3,
          },
        },
      },
      ["effectivity-module-2"] = {
        type = "module",
        name = "effectivity-module-2",
        effect = {
          consumption = {
            bonus = -0.4,
          },
        },
      },
      ["effectivity-module-3"] = {
        type = "module",
        name = "effectivity-module-3",
        effect = {
          consumption = {
            bonus = -0.5,
          },
        },
      },
      ["productivity-module"] = {
        type = "module",
        name = "productivity-module",
        effect = {
          productivity = {
            bonus = 0.04,
          },
          consumption = {
            bonus = 0.4,
          },
          pollution = {
            bonus = 0.05,
          },
          speed = {
            bonus = -0.05,
          },
        },
      },
      ["productivity-module-2"] = {
        type = "module",
        name = "productivity-module-2",
        effect = {
          productivity = {
            bonus = 0.06,
          },
          consumption = {
            bonus = 0.6,
          },
          pollution = {
            bonus = 0.07,
          },
          speed = {
            bonus = -0.1,
          },
        },
      },
      ["productivity-module-3"] = {
        type = "module",
        name = "productivity-module-3",
        effect = {
          productivity = {
            bonus = 0.1,
          },
          consumption = {
            bonus = 0.8,
          },
          pollution = {
            bonus = 0.1,
          },
          speed = {
            bonus = -0.15,
          },
        },
      },
    },
    recipe = {
      recipe = {
        type = "recipe",
        name = "recipe",
        ingredients = {
          { "dummy_1", 5 },
          { "dummy_2", 10 },
        },
        energy_required = 5,
        results = {
          { "dummy_3", 5 },
          { "ash", 3 },
        },
      },
    },
  },
}
defines.prototypes = {
  entity = {
    ["assembling-machine"] = 0,
    beacon = 0,
  },
  item = {
    item = 0,
    module = 0,
  },
}

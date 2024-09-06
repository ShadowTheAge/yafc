data = {
  raw = {
    recipe = {
      steam_void = {
        type = "recipe",
        name = "steam_void",
        category = "oil-processing",
        energy_required = 5,
        ingredients = {
          {
            type = "fluid",
            name = "steam",
            amount = 50,
          },
        },
      },
    },
    fluid = {
      steam = {
        type = "fluid",
        name = "steam",
        default_temperature = 15,
        max_temperature = 1000,
        heat_capacity = "0.2KJ",
        gas_temperature = 15,
        auto_barrel = False,
        icon = "",
      }
    },
    boiler = {
      boiler = {
        type = "boiler",
        name = "boiler",
        mode = "output-to-separate-pipe",
        target_temperature = 165,
        output_fluid_box = {
          filter = "steam",
        },
        energy_consumption = "1.8MW",
        energy_source = {
          type = "burner",
          fuel_category = "chemical",
        },
      },
      ["heat-exchanger"] = {
        type = "boiler",
        name = "heat-exchanger",
        mode = "output-to-separate-pipe",
        target_temperature = 500,
        output_fluid_box = {
          filter = "steam",
        },
        energy_consumption = "10MW",
        energy_source = {
          type = "heat",
          max_temperature = 1000,
          specific_heat = "1MJ",
        },
      },
    },
    generator = {
      ["steam-engine"] = {
        type = "generator",
        name = "steam-engine",
        fluid_usage_per_tick = 0.5,
        maximum_temperature = 165,
        fluid_box = {
          filter = "steam",
          minimum_temperature = 100,
        },
        energy_source = {
          type = "electric",
        },
      },
      ["steam-turbine"] = {
        type = "generator",
        name = "steam-turbine",
        fluid_usage_per_tick = 1,
        maximum_temperature = 500,
        burns_fluid = False,
        fluid_box = {
          filter = "steam",
        },
        energy_source = {
          type = "electric",
        },
      },
    },
  },
}
defines.prototypes = {
  entity = {
    boiler = 0,
    generator = 0,
  },
  item = { },
  fluid = {
    fluid = 0,
  },
}

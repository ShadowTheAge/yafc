data = {
  raw = {
    recipe = {
      -- Recipes so Yafc will split water into the required temperatures
      cold_water = {
        type = "recipe",
        name = "cold_water",
        category = "oil-processing",
        energy_required = 5,
        results = {
          {
            type = "fluid",
            name = "water",
            amount = 50,
            temperature = 15,
          },
        },
      },
      warm_water = {
        type = "recipe",
        name = "warm_water",
        category = "oil-processing",
        energy_required = 5,
        results = {
          {
            type = "fluid",
            name = "water",
            amount = 50,
            temperature = 50,
          },
        },
      },
      hot_water = {
        type = "recipe",
        name = "hot_water",
        category = "oil-processing",
        energy_required = 5,
        results = {
          {
            type = "fluid",
            name = "water",
            amount = 50,
            temperature = 90,
          },
        },
      },
    },
    item = {
      coal = {
        type = "item",
        name = "coal",
        fuel_category = "chemical",
        fuel_value = "4MJ",
      },
    },
    fluid = {
      water = {
        type = "fluid",
        name = "water",
        default_temperature = 15,
        max_temperature = 1000,
        heat_capacity = "0.2KJ",
        icon = "",
      },
      steam = {
        type = "fluid",
        name = "steam",
        default_temperature = 15,
        max_temperature = 1000,
        heat_capacity = "0.2KJ",
        gas_temperature = 15,
        auto_barrel = False,
        icon = "",
      },
    },
    boiler = {
      boiler = {
        type = "boiler",
        name = "boiler",
        mode = "output-to-separate-pipe",
        target_temperature = 165,
        fluid_box = {
          filter = "water",
        },
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
        fluid_box = {
          filter = "water",
        },
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
  },
}
defines.prototypes = {
  entity = {
    boiler = 0,
  },
  item = {
    item = 0,
  },
  fluid = {
    fluid = 0,
  },
}

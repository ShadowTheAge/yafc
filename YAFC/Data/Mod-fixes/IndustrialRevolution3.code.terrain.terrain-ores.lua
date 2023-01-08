local util = ...;

for _,gem in pairs({"diamond","ruby"}) do -- gem-rock-electrum exists in the mod, but isn't placed on the map
	if data.raw["simple-entity"]["gem-rock-"..gem] and not data.raw["simple-entity"]["gem-rock-"..gem].autoplace then
		data.raw["simple-entity"]["gem-rock-"..gem].autoplace = {}
	end
end

return util;
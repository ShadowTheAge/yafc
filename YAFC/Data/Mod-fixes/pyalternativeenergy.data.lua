-- Remove the placer entities that pyanodon uses for some runtime scripting trickery.
for _, item in pairs(data.raw.item) do
	if item.place_result then
		item.place_result = item.place_result:gsub("%-placer$", ""):gsub("%-placer%-", "-")
	end
	if item.next_upgrade then
		item.next_upgrade = item.next_upgrade:gsub("%-placer$", ""):gsub("%-placer%-", "-")
	end
end

for _, type in pairs(data.raw) do
	for name, _ in pairs(type) do
		if name:match("%-placer$") or name:match("%-placer%-") then
			type[name] = nil
		end
	end
end
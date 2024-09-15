-- This file is the first to run on fresh lua state, before all mod files
-- Should setup sandboxing and other data that factorio mods expect
-- "require", "raw_log", "mods" and "settings" are already set up here

local unsetGlobal = {"getfenv","load","loadfile","loadstring","setfenv","coroutine","module","package","io","os","newproxy"};

for i=1,#unsetGlobal do
	_G[unsetGlobal] = nil;
end

local parentTypes = {};

defines = require("Defines");
for defineType,typeTable in pairs(defines.prototypes) do
	for subType,_ in pairs(typeTable) do
		if (defineType ~= subType) then
			parentTypes[subType] = defineType;
		end
	end
end	

local function dataAdd(type, name, obj)
	if not data.raw[type] then data.raw[type] = {} end;
	data.raw[type][name] = obj;
	--[[if parentTypes[type] then
		dataAdd(parentTypes[type], name, obj);
	end]]
end

data = {raw = {}, is_demo=false}
function data:extend(t)
	for i=1,#t do
		local prototype = t[i];
		dataAdd(prototype.type, prototype.name, prototype);
	end
end

local raw_log = _G.raw_log;
_G.raw_log = nil;
function log(s)
	if type(s) ~= "string" then s = serpent.block(s) end;
	raw_log(s);
end

serpent = require("Serpent")

table_size = function(t)
	local count = 0
	for k,v in pairs(t) do
		count = count + 1
	end
	return count
end
			
data.data_crawler = "yafc "..yafc_version;

log(data.data_crawler);

size=32;
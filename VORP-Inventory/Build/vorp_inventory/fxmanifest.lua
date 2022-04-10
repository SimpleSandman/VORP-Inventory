game 'rdr3'

fx_version 'cerulean'
rdr3_warning 'I acknowledge that this is a prerelease build of RedM, and I am aware my resources *will* become incompatible once RedM ships.'

ui_page 'html/ui.html'

client_scripts {'*.Client.net.dll', 'client/client.lua'}
server_scripts {'*.Server.net.dll', 'vorpInventoryAPI.lua', 'server/server.lua'}

shared_script 'config.lua'

server_exports {'vorp_inventoryApi'}

files {'Newtonsoft.Json.dll', 'html/**/*', 'config.json', 'languages/**/*'}

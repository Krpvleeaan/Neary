using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var store = new ConcurrentDictionary<string, LocationRecord>();

var builder = WebApplication.CreateSlimBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var app = builder.Build();

app.MapPost("/api/location", (LocationRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.UserId))
        return Results.BadRequest("UserId is required");

    var record = new LocationRecord(req.Lat, req.Lon, req.Battery, DateTime.UtcNow);
    store.AddOrUpdate(req.UserId, record, (_, _) => record);

    return Results.Ok();
});

app.MapGet("/api/locations", () => store);

app.MapGet("/api/location/{userId}", (string userId) =>
{
    return store.TryGetValue(userId, out var record)
        ? Results.Ok(record)
        : Results.NotFound();
});

app.MapGet("/", () => Results.Content(MapPage.Html, "text/html"));

app.Run("http://0.0.0.0:5000");

record LocationRequest(string UserId, double Lat, double Lon, double Battery);
record LocationRecord(double Lat, double Lon, double Battery, DateTime UpdatedAt);

[JsonSerializable(typeof(LocationRequest))]
[JsonSerializable(typeof(LocationRecord))]
[JsonSerializable(typeof(ConcurrentDictionary<string, LocationRecord>))]
internal partial class AppJsonContext : JsonSerializerContext;

static class MapPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no"/>
<title>Neary</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body{height:100%;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0D1117;color:#E6EDF3}
#map{position:fixed;inset:0;z-index:0}
.leaflet-tile-pane{filter:brightness(.7) invert(1) contrast(1.1) hue-rotate(200deg) saturate(.4) brightness(.8)}
.leaflet-control-attribution,.leaflet-control-zoom{display:none!important}

.panel{position:fixed;top:16px;left:16px;z-index:1000;
  background:rgba(22,27,34,.92);backdrop-filter:blur(20px);-webkit-backdrop-filter:blur(20px);
  border:1px solid rgba(255,255,255,.08);border-radius:16px;padding:20px;
  width:320px;max-width:calc(100vw - 32px);
  box-shadow:0 8px 32px rgba(0,0,0,.4)}
.panel h1{font-size:18px;font-weight:600;margin-bottom:4px;color:#E6EDF3}
.panel .sub{font-size:12px;color:#484F58;margin-bottom:16px}

.search-box{display:flex;gap:8px;margin-bottom:12px}
.search-box input{flex:1;padding:10px 14px;border-radius:10px;border:1px solid #30363D;
  background:#0D1117;color:#E6EDF3;font-size:14px;outline:none;transition:border .2s}
.search-box input:focus{border-color:#3B82F6}
.search-box input::placeholder{color:#484F58}
.search-box button{padding:10px 16px;border-radius:10px;border:none;
  background:#3B82F6;color:#fff;font-size:14px;font-weight:600;cursor:pointer;
  transition:background .2s;white-space:nowrap}
.search-box button:hover{background:#2563EB}

.info{display:none;padding:14px;background:rgba(13,17,23,.7);border-radius:12px;border:1px solid #21262D}
.info.active{display:block}
.info-row{display:flex;justify-content:space-between;padding:6px 0;font-size:13px;border-bottom:1px solid #21262D}
.info-row:last-child{border-bottom:none}
.info-row .label{color:#8B949E}
.info-row .value{color:#E6EDF3;font-weight:500;font-variant-numeric:tabular-nums}
.info .status{margin-top:10px;font-size:11px;color:#484F58;text-align:center}

.users-list{margin-top:12px}
.users-list summary{font-size:12px;color:#8B949E;cursor:pointer;user-select:none}
.users-list summary:hover{color:#E6EDF3}
.user-btn{display:block;width:100%;text-align:left;padding:8px 12px;margin-top:4px;
  border-radius:8px;border:1px solid #21262D;background:transparent;
  color:#E6EDF3;font-size:13px;cursor:pointer;transition:background .15s}
.user-btn:hover{background:#161B22}
.user-btn .uid{font-weight:600}
.user-btn .meta{color:#484F58;font-size:11px;margin-top:2px}
.no-users{color:#484F58;font-size:12px;padding:8px 0}

@media(max-width:400px){.panel{width:auto;left:8px;right:8px;padding:16px}}
</style>
</head>
<body>
<div id="map"></div>

<div class="panel">
  <h1>Neary</h1>
  <div class="sub">Location Tracker</div>

  <div class="search-box">
    <input id="uid" type="text" placeholder="Введите ID"/>
    <button onclick="track()">Найти</button>
  </div>

  <div id="info" class="info">
    <div class="info-row"><span class="label">Широта</span><span class="value" id="lat">—</span></div>
    <div class="info-row"><span class="label">Долгота</span><span class="value" id="lon">—</span></div>
    <div class="info-row"><span class="label">Батарея</span><span class="value" id="bat">—</span></div>
    <div class="info-row"><span class="label">Обновлено</span><span class="value" id="upd">—</span></div>
    <div class="status" id="status"></div>
  </div>

  <div class="users-list">
    <details>
      <summary>Все пользователи</summary>
      <div id="ulist"></div>
    </details>
  </div>
</div>

<script>
var map=L.map('map',{zoomControl:false,attributionControl:false}).setView([55.75,37.62],4);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19}).addTo(map);

var marker=null,circle=null,trackId=null,timer=null;

function track(){
  var id=document.getElementById('uid').value.trim();
  if(!id)return;
  trackId=id;
  fetch('/api/location/'+encodeURIComponent(id))
    .then(r=>{if(!r.ok)throw new Error('not found');return r.json()})
    .then(d=>showLocation(d))
    .catch(()=>{
      document.getElementById('info').className='info active';
      document.getElementById('lat').textContent='—';
      document.getElementById('lon').textContent='—';
      document.getElementById('bat').textContent='—';
      document.getElementById('upd').textContent='—';
      document.getElementById('status').textContent='Пользователь «'+id+'» не найден';
      if(marker){map.removeLayer(marker);marker=null}
      if(circle){map.removeLayer(circle);circle=null}
    });
  clearInterval(timer);
  timer=setInterval(()=>{if(trackId)fetch('/api/location/'+encodeURIComponent(trackId))
    .then(r=>r.ok?r.json():null).then(d=>{if(d)showLocation(d)})
    .catch(()=>{})},15000);
}

function showLocation(d){
  var ll=[d.lat,d.lon];
  if(!marker){
    circle=L.circleMarker(ll,{radius:20,color:'#3B82F6',fillColor:'#3B82F6',fillOpacity:.15,weight:1.5,opacity:.3}).addTo(map);
    marker=L.circleMarker(ll,{radius:7,color:'#fff',fillColor:'#3B82F6',fillOpacity:1,weight:2.5}).addTo(map);
    map.setView(ll,15,{animate:true});
  }else{
    marker.setLatLng(ll);circle.setLatLng(ll);
    map.setView(ll,map.getZoom()<13?15:map.getZoom(),{animate:true});
  }
  document.getElementById('info').className='info active';
  document.getElementById('lat').textContent=d.lat.toFixed(6);
  document.getElementById('lon').textContent=d.lon.toFixed(6);
  document.getElementById('bat').textContent=d.battery>=0?d.battery+'%':'—';
  var t=new Date(d.updatedAt);
  document.getElementById('upd').textContent=t.toLocaleString('ru-RU');
  document.getElementById('status').textContent='Обновляется каждые 15 сек';
}

function selectUser(id){
  document.getElementById('uid').value=id;
  track();
}

function loadUsers(){
  fetch('/api/locations').then(r=>r.json()).then(data=>{
    var el=document.getElementById('ulist');
    var keys=Object.keys(data);
    if(!keys.length){el.innerHTML='<div class="no-users">Нет данных</div>';return}
    el.innerHTML=keys.map(k=>{
      var d=data[k],t=new Date(d.updatedAt);
      return '<button class="user-btn" onclick="selectUser(\''+k+'\')"><div class="uid">'+k+'</div><div class="meta">'+t.toLocaleString('ru-RU')+(d.battery>=0?' · '+d.battery+'%':'')+'</div></button>';
    }).join('');
  }).catch(()=>{});
}

document.getElementById('uid').addEventListener('keydown',function(e){if(e.key==='Enter')track()});
loadUsers();setInterval(loadUsers,30000);
</script>
</body>
</html>
""";
}

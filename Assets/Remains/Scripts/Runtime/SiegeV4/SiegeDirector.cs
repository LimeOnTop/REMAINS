using UnityEngine;
using UnityEngine.InputSystem;
namespace Remains {
 [DefaultExecutionOrder(50)]
 public class SiegeDirector:MonoBehaviour {
  public static SiegeDirector Instance;public CastleWalkthrough player;public Transform blacksmith;public Transform[] smiths;public SiegeEngineV4[] engines;public GameObject swordPrefab;public GameObject[] weaponPrefabs;public SiegeEngineV4 selectedEngine;public SiegeBridge bridge;public SiegeTarget gate;
  public bool menu;public SiegeEngineV4 held;public string notice="Доберитесь до кузнеца у южного шатра";float noticeUntil;GameObject weapon;float swing;LineRenderer arc;
  public static bool BlockMovement=>Instance&&(Instance.menu||Instance.held);
  public static bool BlockLook=>Instance&&Instance.menu;
  void Awake(){Instance=this;Application.runInBackground=true;}
  void OnDestroy(){if(Instance==this)Instance=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  void Start(){var g=new GameObject("Siege_Aiming_Arc");arc=g.AddComponent<LineRenderer>();arc.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));arc.sharedMaterial.color=new Color(.85f,.72f,.37f);arc.startWidth=.035f;arc.endWidth=.02f;arc.enabled=false;}
  public void Message(string s){notice=s;noticeUntil=Time.time+4;}
  bool NearSmith(){foreach(var s in smiths)if(s&&Vector3.Distance(player.transform.position,s.position)<4)return true;return false;}
  void Update(){var k=Keyboard.current;var m=Mouse.current;if(!player||k==null)return;
   if(menu){if(k.escapeKey.wasPressedThisFrame||k.eKey.wasPressedThisFrame)CloseMenu();return;}
   if(k.eKey.wasPressedThisFrame){
    if(held){held.aiming=false;held=null;arc.enabled=false;return;}
    if(NearSmith()){menu=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
    foreach(var e in engines)if(e&&e.gameObject.activeSelf&&e.CanGrab(player.transform.position)){held=e;Message(e.kind==SiegeEngineKind.Ram?"Разгонитесь не менее 5 м; E — отпустить":"ПКМ — прицеливание; ЛКМ — выстрел; E — отпустить");break;}
   }
   if(held){
    if(k.escapeKey.wasPressedThisFrame){held.aiming=false;held=null;arc.enabled=false;return;}
    if(m!=null&&m.rightButton.wasPressedThisFrame&&held.kind!=SiegeEngineKind.Ram)held.aiming=!held.aiming;
    float forward=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0),steer=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);held.Drive(forward,steer,player.transform,Time.deltaTime);
    var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.position=held.Grip+Vector3.up*.05f;c.enabled=true;
    arc.enabled=held.aiming;if(held.aiming&&held.AimVelocity(player.viewCamera,player.transform,out var v)){arc.positionCount=48;for(int i=0;i<48;i++){float t=i*(held.kind==SiegeEngineKind.Ballista?.04f:.16f);arc.SetPosition(i,held.muzzle.position+v*t+Physics.gravity*.5f*t*t);}}else arc.enabled=false;
    if(m!=null&&m.leftButton.wasPressedThisFrame)held.Fire(player.viewCamera,player.transform);
   }else if(weapon){swing=Mathf.MoveTowards(swing,0,Time.deltaTime*3);weapon.transform.localRotation=Quaternion.Euler(-25+Mathf.Sin(swing*Mathf.PI)*85,0,-15);if(m!=null&&m.leftButton.wasPressedThisFrame&&swing==0){swing=1;Message("Меч: удар. Осадные цели требуют орудий.");}}
  }
  void CloseMenu(){menu=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
  public void SelectEngine(int index){var e=engines[index];e.gameObject.SetActive(true);selectedEngine=e;Message(e.Title+" готова на площадке перед лагерем");CloseMenu();}
  void EquipWeapon(int index){if(weapon)Destroy(weapon);weapon=Instantiate(weaponPrefabs!=null&&weaponPrefabs.Length>index?weaponPrefabs[index]:swordPrefab,player.viewCamera.transform);weapon.SetActive(true);weapon.transform.localPosition=new Vector3(.42f,-.45f,.8f);weapon.transform.localScale=Vector3.one*.7f;foreach(var c in weapon.GetComponentsInChildren<Collider>())Destroy(c);CloseMenu();Message((index==0?"Меч":"Молот")+" экипирован — ЛКМ удар");}
  void Bar(Vector3 world,float health,string title){var c=player.viewCamera;var p=c.WorldToScreenPoint(world);if(p.z<0||p.x<0||p.x>Screen.width)return;float x=p.x-60,y=Screen.height-p.y;GUI.color=new Color(.08f,.09f,.10f,.92f);GUI.DrawTexture(new Rect(x,y,120,10),Texture2D.whiteTexture);GUI.color=health>.34f?new Color(.70f,.63f,.28f):new Color(.78f,.24f,.15f);GUI.DrawTexture(new Rect(x+2,y+2,116*health,6),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(x-20,y-22,180,22),title);}
  void OnGUI(){if(!player)return;var style=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter};
   if(menu){float w=560,h=465,x=(Screen.width-w)/2,y=(Screen.height-h)/2;GUI.Box(new Rect(x,y,w,h),"АРСЕНАЛ КУЗНЕЦА");GUI.Label(new Rect(x+24,y+35,w-48,28),"Выберите снаряжение. Орудия ждут на площадке.",style);
    if(GUI.Button(new Rect(x+25,y+80,w-50,48),"МЕЧ  ·  личное оружие"))EquipWeapon(0);
    if(GUI.Button(new Rect(x+25,y+140,w-50,48),"БОЕВОЙ МОЛОТ  ·  личное оружие"))EquipWeapon(1);
    string[] labels={"БАЛЛИСТА  ·  болты для цепей моста","ТАРАН  ·  разгон и удар по воротам","КАТАПУЛЬТА  ·  камни для проломов"};for(int i=0;i<3;i++)if(GUI.Button(new Rect(x+25,y+205+i*60,w-50,48),labels[i]))SelectEngine(i);
    if(GUI.Button(new Rect(x+25,y+395,w-50,42),"Закрыть · E / Esc"))CloseMenu();return;
   }
   GUI.Label(new Rect(Screen.width/2-10,Screen.height/2-12,20,24),"+",style);
   string hint=held?held.Title+"  |  WASD — движение/поворот  |  E — отпустить\n"+(held.kind==SiegeEngineKind.Ram?$"Скорость {held.speed:0.0} / 2.8 м/с   Разбег {held.runup:0.0} / 5 м":$"ПКМ — {(held.aiming?"закончить прицеливание":"прицелиться")}  |  ЛКМ — выстрел  |  Перезарядка {held.cooldown:0.0} с"):NearSmith()?"E — поговорить с кузнецом · открыть арсенал":"WASD — движение · Shift — бег · E — взаимодействие";
   if(!held)foreach(var e in engines)if(e&&e.gameObject.activeSelf&&e.CanGrab(player.transform.position)){hint="E — ухватиться · "+e.Title;break;}
   GUI.Box(new Rect((Screen.width-760)/2,Screen.height-95,760,75),hint);
   GUI.Label(new Rect(20,20,650,30),"ОСАДА  ·  цепи → мост → ворота",style);if(Time.time<noticeUntil)GUI.Label(new Rect((Screen.width-800)/2,70,800,35),notice,style);
   if(selectedEngine&&!held){var p=player.viewCamera.WorldToScreenPoint(selectedEngine.transform.position+Vector3.up*3);if(p.z>0)GUI.Label(new Rect(p.x-120,Screen.height-p.y-15,240,30),selectedEngine.Title+" · "+Vector3.Distance(player.transform.position,selectedEngine.transform.position).ToString("0")+" м",style);}
   if(bridge)for(int i=0;i<2;i++)Bar(bridge.chains[i].transform.position+Vector3.up*1.2f,bridge.chains[i].Health,$"Цепь {i+1} · {3-bridge.chains[i].hits}/3");
   if(gate&&Vector3.Distance(player.transform.position,gate.transform.position)<55)Bar(gate.transform.position+Vector3.up*5,gate.Health,$"Засов · {10-gate.hits}/10");
  }
 }
}

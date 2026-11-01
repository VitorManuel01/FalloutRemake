using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview : Node3D
{
	private MapDocument? _map;
	private PreviewLayout? _layout;
	private readonly PreviewHud _hud = new();
	private readonly Camera3D _camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 0.1f, Far = 700, Size = 32 };
	private Node3D _world = new();
	private MultiMeshInstance3D? _floor;
	private MeshInstance3D? _highlight;
	private MeshInstance3D? _arrow;
	private readonly Dictionary<ulong, PreviewObject> _lookup = new();
	private readonly Dictionary<string, StaticBody3D> _bodies = new();
	private readonly Dictionary<PreviewKind, StandardMaterial3D> _materials = new();
	private readonly Dictionary<PreviewKind, Mesh> _meshes = new();
	private readonly Dictionary<PreviewKind, BoxShape3D> _shapes = new();
	private readonly ResourceScope _resources = new();
	private PreviewObject? _selected;
	private Vector3 _focus;
	private float _yaw = Mathf.DegToRad(40);
	private int _elevation;
	private bool _test;
	private bool _sceneReviewTest;
	private string? _capture;
	private string? _directory;
	private bool _shuttingDown;
	private WorldSession? _campaign;
	private MapCatalog? _mapCatalog;
	private string SettingsPath => ProjectSettings.GlobalizePath("res://.local/preview-settings.json");

	public override async void _Ready()
	{
		try
		{
			GetTree().AutoAcceptQuit = false;
			// Graphical windows lay out in real pixels instead of shrinking a fixed 900px HUD.
			// Headless retains the configured logical viewport (its native dummy window is 64px).
			if (DisplayServer.GetName() != "headless")
			{
				GetWindow().ContentScaleSize = Vector2I.Zero;
				GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
			}
			AddChild(_camera); _camera.Current = true;
			AddChild(_hud); AddChild(_world);
			_environment = _resources.Own(new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("101918"),
				AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("b2c4b7"), AmbientLightEnergy = 0.65f,
				ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled, TonemapMode = Godot.Environment.ToneMapper.Filmic
			});
			AddChild(new WorldEnvironment { Environment = _environment });
			_sun = new DirectionalLight3D { LightColor = new Color("f0d8ac"), LightEnergy = 1.3f,
				RotationDegrees = new Vector3(-55, -25, 0), ShadowEnabled = true, DirectionalShadowMaxDistance = 180 };
			AddChild(_sun);
			LoadKit(); ApplyLightingMode();
			_hud.ArchitectureRequested = SetArchitecture; _hud.Architecture(ArchitectureActive, _kit != null, _kitStatus);
			_hud.EntryRequested = GoToEntry; _hud.FrameRequested = FrameAll; _hud.OpenRequested = ChooseFolder;
			_hud.ElevationRequested = elevation => TryInspectionElevation(elevation);
			_hud.FilterChanged = (_, _) => ApplyFilters(); _hud.CycleRequested = CycleSelection;
			_hud.WalkModeRequested = ToggleWalkMode; _hud.ResetWalkerRequested = ResetWalker;
			_hud.DoorUseRequested = UseSelectedDoor;
			_hud.LockpickRequested = UseSelectedLockpick;
			_hud.ExitRequested = UseExit;
			_hud.SaveRequested = SaveCheckpoint; _hud.LoadRequested = LoadCheckpoint;
			_hud.CharacterFocusRequested = FocusCharacter; _hud.CharacterSheetRequested = OpenCharacterWindow; _hud.SkillSheetRequested = OpenSkillSheet;
			_hud.OriginalFloorRequested = ChangeOriginalFloor;
			_hud.NatureModelsRequested = ChangeNatureModels;
			_hud.StopRequested = StopWalking;
			_hud.RevealActorRequested = ChangeRevealActor;
			_hud.InventoryRequested = OpenInventory;
			_hud.NavigationGridRequested = ChangeNavigationGrid;
			_hud.FrameRouteRequested = FrameRoute;
			_hud.LootRequested = OpenLoot;
			_hud.BagRequested = OpenBag;
			_hud.UseClockRequested = OpenUseClock;
			_hud.ContainerLocateRequested = LocateContainer;
			_hud.CombatEndTurnRequested = EndCombatTurn;
			_hud.CombatLeaveRequested = LeaveCombat;
			string[] args = OS.GetCmdlineUserArgs();
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--data-dir" && i + 1 < args.Length) _directory = args[++i];
				else if (args[i] == "--capture" && i + 1 < args.Length) _capture = args[++i];
				else if (args[i] == "--preview-test") _test = true;
				else if (args[i] == "--nature-test") { _test = true; _natureTest = true; }
				else if (args[i] == "--explosive-preparation-test") { _test = true; _explosivePreparationTest = true; }
				else if (args[i] == "--scene-review-test") { _test = true; _sceneReviewTest = true; }
				else if (args[i] == "--visual-preferences-smoke") { _test = true; _preferencesStartupTest = true; }
				else throw new ArgumentException($"Argumento desconhecido: {args[i]}");
			}
			// Scripted checks predate combat and walk past ants; CheckCombat turns encounters on for itself.
			_combatEncounters = !_test;
			string? preferencesNotice = RestoreVisualPreferences();
			if (_directory == null && File.Exists(SettingsPath))
				_directory = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath))?.GetValueOrDefault("GameDataDirectory");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (_shuttingDown) return;
			if (_directory != null) LoadMap(_directory);
			else _hud.Status("Escolha a pasta de idioma do Fallout 2 para começar.");
			if (preferencesNotice != null) NavigationStatus(preferencesNotice);
			// A person starts by choosing a character; automated runs keep the neutral test profile.
			if (_map != null && !_test && _capture == null) OpenCharacterWindow();
			if (_test || _capture != null)
			{
				if (_map == null) throw new InvalidDataException("Preview test requires valid game data.");
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				if (_preferencesStartupTest) { await CheckVisualPreferencesStartup(); await Shutdown(0); return; }
				if (_natureTest) { await CheckNatureModels(); await Shutdown(0); return; }
				if (_explosivePreparationTest) { await CheckPlacedUsePreview(); await Shutdown(0); return; }
				if (_sceneReviewTest) { await CheckEntityReadability(); await Shutdown(0); return; }
				if (_test) await CheckPreview();
				if (_test) await CheckTempleArchitecture();
				if (_test) await CheckNavigation();
				if (_test) await CheckDoors();
				if (_test) await CheckCombat();
				if (_test) await CheckCombatLeave();
				if (_test) await CheckPoison();
				if (_test) await CheckWander();
				if (_test) await CheckCriticals();
				if (_test) await CheckLockpick();
				if (_test) await CheckTravel();
				if (_test) await CheckWorldTravel();
				if (_test) await CheckCheckpoint();
				if (_test) await CheckCharacterCreation();
				if (_test) await CheckCharacterCamera();
				if (_test) await CheckOriginalFloor();
				if (_test) await CheckStopWalking();
				if (_test) await CheckElevationWalkGuard();
				if (_test) await CheckWallOcclusion();
				if (_test) await CheckObjectText();
				if (_test) await CheckInventoryInspector();
				if (_test) await CheckNavigationGrid();
				if (_test) await CheckRouteHover();
				if (_test) await CheckDoorRouteHover();
				if (_test) await CheckExitRouteHover();
				if (_test) await CheckRouteCamera();
				if (_test) await CheckItemCheckpoint();
				if (_test) await CheckContainerLoot();
				if (_test) await CheckPlayerBag();
				if (_test) await CheckItemReturn();
				if (_test) await CheckContainerRouteHover();
				if (_test) await CheckBagOrigin();
				if (_test) await CheckCarryWeightViews();
				if (_test) await CheckQuantityTransfer();
				if (_test) await CheckScriptedContainer();
				if (_test) await CheckInventoryArtUi();
				if (_test) await CheckPlacedUsePreview();
				if (_test) await CheckEntityReadability();
				if (_test) await CheckVisualPreferences();
				if (_capture != null)
				{
					FocusEntry();
					await Capture(_capture);
				}
				if (_test) _Notification((int)NotificationWMCloseRequest);
				else await Shutdown(0);
			}
		}
		catch (Exception error) { Fail(error); }
	}

	private void LoadMap(string directory)
	{
		var data = new GameData(directory);
		var catalog = new MapCatalog(data.Read("data/maps.txt"));
		var runtimePrototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
		var campaign = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 }, runtimePrototype: runtimePrototypes.Get,
			templeProfile: map => TempleNpcDepartureProfile.Create(map,data.Read),arroyoProfile: map => ArroyoRewardProfile.Create(map,data.Read),
			premade: path => CharacterFile.Read(data.Read(path)), aiPackets: _aiPackets = AiPacketCatalog.Read(data.Read("data/ai.txt").Bytes));
		_campaign = campaign; _mapCatalog = catalog; _directory = directory;
		_floorArt = new FloorArtCatalog(data.Read); _artLists = new ResourceCatalog(data.Read);
		_inventoryArt = new InventoryArtCatalog(data.Read); _unavailableItemArt.Clear();
		LoadObjectText(data); LoadCharacters(data);
		BindCurrentArea();
		BindCharacter();
		_elevation = _map!.Prefix.Header.EnteringElevation; _hud.Elevation(_elevation);
		BuildWorld(); FocusEntry();
	}
	private void BindCurrentArea()
	{
		WorldArea area = _campaign!.Current;
		_map = area.Map; _doors = area.Doors; _travel = area.Travel;
		_hud.Area(_mapCatalog!.Get(_campaign.CurrentMap).Name.ToUpperInvariant());
		BindStatus();
	}
	private void BindStatus()
	{
		if (_map == null || _campaign == null) return;
		_hud.Status($"{_map.Prefix.Header.Name} · mapa {_campaign.CurrentMap} · {(ArchitectureActive ? "arquitetura do kit 3D" : "formas provisórias")}");
	}

	private void BuildWorld()
	{
		if (_map == null) return;
		CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseLoot(); CloseBag(); CloseInventory();
		_worldLabels.Clear();
		ResetNpcRouteView();
		using var resources = new ResourceScope();
		_placedUseLayer = null; _placedUseNodes.Clear();
		_replacementNodes.Clear();
		_containerVisuals.Clear(); _containerVisualRevision = -1;
		RemoveChild(_world); _world.QueueFree(); _world = new Node3D { Name = "SourceMap" }; AddChild(_world);
		_lookup.Clear(); _bodies.Clear(); _selected = null;
		_layout = PreviewLayout.Create(_map, _elevation);
		_layout = ProjectTempleLayout(_layout);
		_walls = _layout.Objects.Where(o => o.Kind == PreviewKind.Wall).ToArray();
		_cutWalls.Clear(); _occlusionDirty = true;
		var mesh = resources.Own(new BoxMesh { Size = new Vector3((float)FalloutGrid.FloorWidth * .985f, .08f, 1.97f) });
		var floorInstances = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
			Mesh = mesh, InstanceCount = _layout.Floors.Length });
		for (int i = 0; i < _layout.Floors.Length; i++)
		{
			PreviewFloor floor = _layout.Floors[i];
			floorInstances.SetInstanceTransform(i, new Transform3D(Basis.Identity, Point(floor.Position, -.065f)));
			float shade = .26f + ((floor.Raw & 0xFFF) % 7) * .012f;
			floorInstances.SetInstanceColor(i, new Color(shade * .94f, shade, shade * .91f));
		}
		_floor = new MultiMeshInstance3D { Name = "OriginalFloorCells", Multimesh = floorInstances,
			MaterialOverride = resources.Own(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1 }), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		_world.AddChild(_floor);
		_originalFloor = null;
		foreach (PreviewObject obj in _layout.Objects)
		{
			Vector3 size = Dimensions(obj.Kind);
			var body = new StaticBody3D { Name = $"Object_{obj.Source.Offset}", Position = Point(obj.Position, size.Y / 2), CollisionLayer = 1, CollisionMask = 0 };
			body.SetMeta("source_key", obj.Source.Key); body.SetMeta("source_id", obj.Source.Header.Id);
			body.SetMeta("pid", obj.Source.Header.Pid); body.SetMeta("fid", obj.Source.Header.Fid); body.SetMeta("hex", obj.Source.Header.Tile);
			body.AddChild(new MeshInstance3D { Mesh = ObjectMesh(obj.Kind), MaterialOverride = Material(obj.Kind) });
			if (!_shapes.TryGetValue(obj.Kind, out BoxShape3D? shape)) _shapes[obj.Kind] = shape = _resources.Own(new BoxShape3D { Size = size });
			body.AddChild(new CollisionShape3D { Shape = shape });
			ConfigureEntityVisual(obj, body);
			ConfigureActiveTempleBody(obj,body);
			_world.AddChild(body); _lookup.Add(body.GetInstanceId(), obj); _bodies.Add(obj.Source.Key, body);
		}
		BuildArchitecture();
		ApplyCombatCorpses();
		_highlight = new MeshInstance3D { Name = "Selection", Mesh = resources.Own(new CylinderMesh { TopRadius = .72f, BottomRadius = .72f, Height = .025f, RadialSegments = 48 }),
			MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color(.9f, .75f, .36f, .4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false }), Visible = false };
		_world.AddChild(_highlight);
		_arrow = new MeshInstance3D { MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("f1d28d"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true }) };
		_world.AddChild(_arrow);
		if (_elevation == _map.Prefix.Header.EnteringElevation)
		{
			var beacon = new MeshInstance3D { Position = new Vector3(0, .1f, 0), Mesh = resources.Own(new CylinderMesh { TopRadius = .63f, BottomRadius = .63f, Height = .15f, RadialSegments = 32 }),
				MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("9dd9a0"), EmissionEnabled = true, Emission = new Color("57795a"), EmissionEnergyMultiplier = .6f }) };
			_world.AddChild(beacon);
			_world.AddChild(new Label3D { Text = "ENTRADA", Position = new Vector3(0, 1.45f, 0), FontSize = 40, PixelSize = .012f,
				Modulate = new Color("b6edb4"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true });
		}
		BuildNatureModels();
		ClearSelection();
		ApplyFilters();
		BuildNavigation();
		SetOriginalFloor(_useOriginalFloor);
		RefreshPlacedUses();
		RefreshContainerVisuals(true);
		BuildAppliedSceneryVisuals();
	}

	// Kit walls stand TempleArchitecture.WallHeight; picking boxes and the cutaway test follow the drawn height.
	private Vector3 Dimensions(PreviewKind kind) => kind switch
	{
		PreviewKind.Wall => ArchitectureActive ? new Vector3(.86f, (float)TempleArchitecture.WallHeight, .86f) : new Vector3(.88f, .9f, .88f),
		PreviewKind.Door => new Vector3(.85f, ArchitectureActive ? 1.95f : 1.3f, .30f),
		PreviewKind.Critter => new Vector3(.5f, .9f, .5f), PreviewKind.Item => new Vector3(.35f, .25f, .35f),
		PreviewKind.Exit => new Vector3(1.05f, .09f, 1.05f), PreviewKind.Marker => new Vector3(.28f, .12f, .28f),
		_ => new Vector3(.6f, .5f, .6f)
	};
	private StandardMaterial3D Material(PreviewKind kind)
	{
		if (_materials.TryGetValue(kind, out StandardMaterial3D? material)) return material;
		string color = kind switch { PreviewKind.Wall => "ae9c80", PreviewKind.Door => ArchitectureActive ? "8a6438" : "cfad62", PreviewKind.Critter => "c87761",
			PreviewKind.Exit => "59b49e", PreviewKind.Item => "c9c18d", PreviewKind.Marker => "7b87a0", _ => "8d795b" };
		material = _resources.Own(new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = .88f });
		if (kind == PreviewKind.Exit) { material.EmissionEnabled = true; material.Emission = new Color(color); material.EmissionEnergyMultiplier = .35f; }
		_materials.Add(kind, material); return material;
	}
	private Mesh ObjectMesh(PreviewKind kind)
	{
		if (_meshes.TryGetValue(kind, out Mesh? mesh)) return mesh;
		Vector3 size = Dimensions(kind);
		mesh = kind switch
		{
			PreviewKind.Critter => new CapsuleMesh { Radius = .24f, Height = size.Y },
			PreviewKind.Exit or PreviewKind.Marker => new CylinderMesh { TopRadius = size.X / 2, BottomRadius = size.X / 2, Height = size.Y, RadialSegments = 6 },
			PreviewKind.Scenery => new CylinderMesh { TopRadius = .22f, BottomRadius = .30f, Height = size.Y, RadialSegments = 8 },
			_ => new BoxMesh { Size = size }
		};
		_resources.Own(mesh); _meshes.Add(kind, mesh); return mesh;
	}
	private bool VisibleObject(PreviewObject obj)
	{
		if (_campaign?.TempleProgress is { Departure.Phase: NpcDeparturePhase.Hidden } progress && obj.Source.Key == progress.Scripts.Blocks[0].Key) return false;
		if (_campaign?.Current.Scenery.Replaces(obj.Source.Key) == true) return false;
		string filter = obj.Kind switch { PreviewKind.Wall => "wall", PreviewKind.Door or PreviewKind.Exit => "exit",
			PreviewKind.Critter => "critter", PreviewKind.Marker => "marker", _ => "scenery" };
		return _hud.Filters[filter] && (!obj.Hidden || _hud.Filters["hidden"]);
	}
	private void ApplyFilters()
	{
		if (_layout == null || _floor == null) return;
		ApplyFloorVisibility();
		ApplyPlacedUseFilter();
		ApplySceneryReplacementFilter();
		int visible = 0;
		foreach (PreviewObject obj in _layout.Objects)
		{
			bool show = VisibleObject(obj); StaticBody3D body = _bodies[obj.Source.Key];
			body.Visible = show; body.CollisionLayer = show ? 1u : 0u;
			if (show) visible++;
		}
		_hud.Counts($"{_layout.Floors.Length:N0} células de piso\n{visible:N0} / {_layout.Objects.Length:N0} objetos visíveis");
		if (_selected != null && !VisibleObject(_selected)) ClearSelection();
		_occlusionDirty = true; UpdateWallOcclusion();
		RefreshContainerLocator();
		RefreshNpcRouteView();
		ApplyNatureModels();
	}

	public override void _Process(double delta)
	{
		if (_map == null) return;
		RefreshContainerVisuals();
		if (InspectorOpen) return;
		AdvanceTemplePlayback(delta);
		AdvanceWalker(delta);
		// Automated checks run game time themselves (poison, wandering, criticals): the clock is part of the checkpoint, so
		// frames between two captures must not move it.
		if (!_test) AdvanceGameTime(delta);
		if (!_checkingNavigation) UpdateCombat(delta);
		float x = (Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0);
		float z = (Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0);
		Vector3 right = new(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
		Vector3 down = new(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
		if (x != 0 || z != 0) SetCameraFollow(false);
		if (_followActor && !_checkingNavigation && _walkerNode != null && _walker != null) _focus = _walkerNode.Position;
		_focus += (right * x + down * z) * (float)delta * _camera.Size * .55f;
		UpdateCamera();
		UpdateWallOcclusion();
		if (!_checkingNavigation && !InCombat)
		{
			if (Input.IsMouseButtonPressed(MouseButton.Right)) ClearRouteHover();
			else RouteHoverAtScreen(GetViewport().GetMousePosition());
		}
	}
	public override void _UnhandledInput(InputEvent input)
	{
		if (_map == null) return;
		if (InspectorOpen)
		{
			if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.I }) CloseInventory();
			if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.J }) CloseLoot();
			if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B }) CloseBag();
			if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.N }) CloseCharacterWindow();
			if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.H }) CloseSkillSheet();
			return;
		}
		if (HandleCombatInput(input)) return;
		if (input is InputEventKey { Pressed: true, Echo: false } key)
		{
			if (key.PhysicalKeycode == Key.F) GoToEntry();
			if (key.PhysicalKeycode == Key.Home) FrameAll();
			if (key.PhysicalKeycode == Key.Q) _yaw -= Mathf.Pi / 6;
			if (key.PhysicalKeycode == Key.E) _yaw += Mathf.Pi / 6;
			if (key.PhysicalKeycode == Key.M) ToggleWalkMode();
			if (key.PhysicalKeycode == Key.U) UseSelectedDoor();
			if (key.PhysicalKeycode == Key.L) UseSelectedLockpick();
			if (key.PhysicalKeycode is Key.Enter or Key.KpEnter) UseExit();
			if (key.PhysicalKeycode == Key.K) SaveCheckpoint();
			if (key.PhysicalKeycode == Key.R) LoadCheckpoint();
			if (key.PhysicalKeycode == Key.C) FocusCharacter();
			if (key.PhysicalKeycode == Key.N) OpenCharacterWindow();
			if (key.PhysicalKeycode == Key.H) OpenSkillSheet();
			if (key.PhysicalKeycode == Key.O) ChangeOriginalFloor(!_useOriginalFloor);
			if (key.PhysicalKeycode == Key.Escape) StopWalking();
			if (key.PhysicalKeycode == Key.V) ChangeRevealActor(!_revealActor);
			if (key.PhysicalKeycode == Key.I) OpenInventory();
			if (key.PhysicalKeycode == Key.G) ChangeNavigationGrid(!_showNavigationGrid);
			if (key.PhysicalKeycode == Key.T) FrameRoute();
			if (key.PhysicalKeycode == Key.J) OpenLoot();
			if (key.PhysicalKeycode == Key.B) OpenBag();
			if (key.PhysicalKeycode == Key.P) SetArchitecture(!_architectureEnabled);
			if (key.PhysicalKeycode == Key.X) SwitchUnarmedHand();
			UpdateCamera();
		}
		if (_hud.MouseOverPanel) return;
		if (input is InputEventMouseButton { Pressed: true } mouse)
		{
			if (mouse.ButtonIndex == MouseButton.WheelUp) _camera.Size = Mathf.Clamp(_camera.Size * .86f, 8, 220);
			if (mouse.ButtonIndex == MouseButton.WheelDown) _camera.Size = Mathf.Clamp(_camera.Size / .86f, 8, 220);
			if (mouse.ButtonIndex == MouseButton.Left) { if (_walkMode) WalkAtScreen(mouse.Position); else Pick(mouse.Position); }
		}
		if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Right) != 0)
			DragCamera(motion.Relative);
	}
	private void UpdateCamera()
	{
		// Offset the world view left to keep the sidebar from covering the focus point.
		Vector3 right = new(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
		Vector3 target = _focus + right * (_camera.Size * 145 / Math.Max(1, GetViewport().GetVisibleRect().Size.Y));
		_camera.Position = target + new Vector3(Mathf.Sin(_yaw) * 90, 110, Mathf.Cos(_yaw) * 90);
		_camera.LookAt(target, Vector3.Up);
		RefreshWorldLabels();
	}
	private void FocusEntry()
	{
		if (_map == null) return;
		SetCameraFollow(false);
		GroundPoint direction = FalloutGrid.Facing(_map.Prefix.Header.EnteringRotation);
		_focus = new Vector3((float)direction.X * 6, 0, (float)direction.Z * 6); _camera.Size = 32; UpdateCamera();
	}
	private void ChangeElevation(int elevation)
	{
		_elevation = elevation; _hud.Elevation(elevation);
		if (_map != null) { BuildWorld(); FrameAll(); }
	}
	private bool TryInspectionElevation(int elevation)
	{
		if (elevation == _elevation) return true;
		if (CombatRefuses("trocar de elevação")) { _hud.Elevation(_elevation); return false; }
		if (_walker?.Moving == true)
		{
			_hud.Elevation(_elevation);
			NavigationStatus("Termine ou pare a caminhada [Esc] antes de mudar a elevação.");
			return false;
		}
		ChangeElevation(elevation); return true;
	}
	private void GoToEntry()
	{
		if (_map == null) return;
		if (!TryInspectionElevation(_map.Prefix.Header.EnteringElevation)) return;
		FocusEntry();
	}
	private void FrameAll()
	{
		if (_layout == null || _layout.Floors.Length == 0) return;
		SetCameraFollow(false);
		float minX = (float)_layout.Floors.Min(f => f.Position.X), maxX = (float)_layout.Floors.Max(f => f.Position.X);
		float minZ = (float)_layout.Floors.Min(f => f.Position.Z), maxZ = (float)_layout.Floors.Max(f => f.Position.Z);
		_focus = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
		_camera.Size = Math.Clamp(Math.Max(maxX - minX, maxZ - minZ) * 1.45f, 16, 220); UpdateCamera();
	}
	private void Pick(Vector2 screen)
	{
		Vector3 origin = _camera.ProjectRayOrigin(screen), end = origin + _camera.ProjectRayNormal(screen) * 700;
		PreviewObject? obj = InteractionObjectAtScreen(screen);
		if (obj != null) SelectObject(obj);
		else
		{
			Vector3 direction = end - origin;
			float t = -origin.Y / direction.Y;
			Vector3 point = origin + direction * t;
			int? square = _layout == null ? null : FalloutGrid.SquareAt(new GroundPoint(point.X + _layout.Origin.X, point.Z + _layout.Origin.Z));
			PreviewFloor? floor = _hud.Filters["floor"] ? _layout?.Floors.FirstOrDefault(f => f.Square == square) : null;
			ClearSelection();
			if (floor != null) _hud.Select($"Piso · célula {floor.Square}", $"Elevação  {_elevation}\nID de piso  {floor.Raw & 0xFFF}\nFlags  {(floor.Raw >> 12) & 15}\nValor bruto  0x{floor.Raw:X8}\n\nA célula não define, por si só, uma área caminhável.");
		}
	}
	private void SelectObject(PreviewObject obj)
	{
		_selected = obj; ObjectHeader h = obj.Source.Header;
		_highlight!.Position = Point(obj.Position, .04f); _highlight.Visible = true;
		GroundPoint facing = FalloutGrid.Facing(h.Rotation);
		Vector3 start = Point(obj.Position, .1f), tip = start + new Vector3((float)facing.X, 0, (float)facing.Z) * 1.25f;
		using var line = new ImmediateMesh(); line.SurfaceBegin(Mesh.PrimitiveType.Lines);
		line.SurfaceAddVertex(start); line.SurfaceAddVertex(tip);
		Vector3 side = new Vector3(-(float)facing.Z, 0, (float)facing.X) * .18f;
		Vector3 back = (tip - start).Normalized() * .3f;
		line.SurfaceAddVertex(tip); line.SurfaceAddVertex(tip - back + side);
		line.SurfaceAddVertex(tip); line.SurfaceAddVertex(tip - back - side); line.SurfaceEnd(); _arrow!.Mesh = line; _arrow.Visible = true;
		string title = obj.Kind switch { PreviewKind.Wall => "Parede", PreviewKind.Door => "Porta", PreviewKind.Exit => "Saída",
			PreviewKind.Critter => "Criatura", PreviewKind.Item => "Item", PreviewKind.Marker => "Marcador", _ => "Cenário" };
		title = EntityPresentation.Role(obj.Source) switch { EntityVisualRole.Barrier => "Obstáculo", EntityVisualRole.LightSource => "Fonte de luz", EntityVisualRole.Container => "Recipiente", _ => title };
		string text = $"ID  {h.Id}\nPID  {h.Pid}  (0x{h.Pid:X8})\nFID  {h.Fid}  (0x{h.Fid:X8})\nHex  {h.Tile}    Elevação  {h.Elevation}\nRotação  {h.Rotation}    SID  {h.Sid}\nScript  {h.ScriptIndex}\nFlags  0x{h.Flags:X8}\nOffset  {obj.Source.Offset}\nInventário  {obj.Source.InventoryCount} registros\nDeslocamento do sprite  {h.X}, {h.Y}";
		if (obj.Source.State.TryGetValue("destinationMap", out int destination)) text += $"\n\nDestino: {MapDisplayName(destination)} · mapa {destination}";
		foreach (var (field, label) in new[] { ("destinationTile", "Hex de destino"), ("destinationElevation", "Elevação de destino"), ("destinationRotation", "Rotação de destino") })
			if (obj.Source.State.TryGetValue(field, out int value)) text += $"\n{label}: {value}";
		string navigation = NavigationDetails(obj);
		text = obj.Kind == PreviewKind.Door ? navigation.Trim() + "\n\n" + text : text + navigation;
		if (obj.Kind == PreviewKind.Door && _doors?.Get(obj.Source.Key) is { } doorState) title += doorState.IsOpen ? " aberta" : " fechada";
		PrototypeText? original = OriginalText(obj.Source);
		if (original?.Description is { } description) text = description + "\n\n" + text;
		if (original != null) text += $"\n\nTexto original · {_objectText!.Language}\n{original.Source.Path}\nMensagem  {original.MessageId}";
		string entityDescription = EntityDescription(obj);
		if (entityDescription.Length != 0) text = entityDescription + "\n\n" + text;
		_hud.Select(original?.Name is { } name ? $"{ShortName(name)} · {title} · objeto {h.Id}" : $"{title} · objeto {h.Id}", text);
		PreviewObject[] colocated = _layout!.Objects.Where(o => o.Source.Header.Tile == h.Tile && VisibleObject(o)).ToArray();
		int position = Array.FindIndex(colocated, o => o.Source.Key == obj.Source.Key) + 1;
		_hud.CycleAction(colocated.Length > 1);
		if (colocated.Length > 1) _hud.Select(_hud.SelectionHeading, $"{position}/{colocated.Length} no hex · Próximo alterna os objetos originais.\n\n" + _hud.SelectionDetails);
		_hud.InventoryAction(CanInspectInventory(obj.Source));
		RefreshLootAction();
		RefreshDoorAction();
	}
	private void ClearSelection()
	{
		_selected = null;
		_hud.CycleAction(false);
		_hud.InventoryAction(false);
		RefreshLootAction();
		if (_highlight != null) _highlight.Visible = false;
		if (_arrow != null) _arrow.Visible = false;
		_hud.Select("Nenhum elemento selecionado", "Clique em um volume para consultar os IDs e a posição do objeto original.\n\nAs formas e alturas são provisórias.");
		RefreshDoorAction();
	}
	private void CycleSelection()
	{
		if (_selected == null || _layout == null) return;
		PreviewObject[] same = _layout.Objects.Where(o => o.Source.Header.Tile == _selected.Source.Header.Tile && VisibleObject(o)).ToArray();
		int index = Array.FindIndex(same, o => o.Source.Key == _selected.Source.Key);
		if (same.Length > 0) SelectObject(same[(index + 1) % same.Length]);
	}
	private void ChooseFolder()
	{
		var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenDir, Access = FileDialog.AccessEnum.Filesystem,
			Title = "Escolha a pasta que contém master.dat e critter.dat", UseNativeDialog = true };
		AddChild(dialog);
		dialog.DirSelected += path =>
		{
			try { LoadMap(path); Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
				File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["GameDataDirectory"] = path })); }
			catch (Exception error) { Fail(error); }
			dialog.QueueFree();
		};
		dialog.Canceled += () => dialog.QueueFree(); dialog.PopupCenteredRatio(.7f);
	}
	private async void Fail(Exception error)
	{
		GD.PushError(error.Message); _hud.Status("Não foi possível abrir os dados. Use ‘Abrir dados’ para escolher outra pasta.");
		_hud.Select("Dados indisponíveis", error.Message);
		if (_test || _capture != null) await Shutdown(1);
	}
	private static Vector3 Point(GroundPoint point, float y) => new((float)point.X, y, (float)point.Z);

	private async System.Threading.Tasks.Task Capture(string path)
	{
		if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Use a graphical display driver for viewport capture.");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using ViewportTexture texture = GetViewport().GetTexture();
		using Image image = texture.GetImage();
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		Error saved = image.SavePng(path);
		if (saved != Error.Ok) throw new IOException($"Capture failed: {saved}");
		GD.Print($"PREVIEW_CAPTURE_OK: {path}");
	}

	private async System.Threading.Tasks.Task CheckPreview()
	{
		if (_map == null || _layout == null || _layout.Elevation != 0 || _layout.Objects.Length != 1842 || _lookup.Count != 1842)
			throw new InvalidDataException("Elevation 0 preview count mismatch.");
		using var floorInstances = _floor?.Multimesh;
		if (floorInstances?.InstanceCount != _layout.Floors.Length) throw new InvalidDataException("Floor instance mismatch.");
		foreach (PreviewObject obj in _layout.Objects)
		{
			if (_bodies[obj.Source.Key].GetMeta("source_key").AsString() != obj.Source.Key) throw new InvalidDataException("Source metadata mismatch.");
			if (_bodies[obj.Source.Key].GetMeta("source_id").AsInt32() != obj.Source.Header.Id
				|| _bodies[obj.Source.Key].GetMeta("pid").AsInt32() != obj.Source.Header.Pid
				|| _bodies[obj.Source.Key].GetMeta("fid").AsInt32() != obj.Source.Header.Fid
				|| _bodies[obj.Source.Key].GetMeta("hex").AsInt32() != obj.Source.Header.Tile)
				throw new InvalidDataException("Source IDs changed in scene metadata.");
			if (_bodies[obj.Source.Key].Position.DistanceTo(Point(obj.Position, Dimensions(obj.Kind).Y / 2)) > .0001f) throw new InvalidDataException("Object transform mismatch.");
		}
		PreviewObject target = _layout.Objects.First(o => o.Kind == PreviewKind.Door);
		SelectObject(target);
		if (_selected?.Source.Offset != target.Source.Offset) throw new InvalidDataException("Selection metadata mismatch.");
		_focus = Point(target.Position, 0); UpdateCamera();
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		// A vertical ray isolates the original hex, independently of camera orientation.
		Vector3 p = Point(target.Position, 0);
		using var query = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 5, p - Vector3.Up, 1);
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (!hit.TryGetValue("collider", out Variant value) || value.AsGodotObject() is not StaticBody3D hitBody
			|| !_lookup.TryGetValue(hitBody.GetInstanceId(), out PreviewObject? picked) || picked.Source.Header.Tile != target.Source.Header.Tile)
			throw new InvalidDataException("Object picking ray missed.");
		ClearSelection();
		Pick(_camera.UnprojectPosition(p + Vector3.Up * 1.2f));
		if (_selected?.Source.Key != target.Source.Key) throw new InvalidDataException("Camera picking failed to select the door.");
		if (_capture != null) await Capture(Path.ChangeExtension(_capture, "selection.png"));
		_hud.Filters["exit"] = false; ApplyFilters();
		if (_bodies[target.Source.Key].CollisionLayer != 0 || _bodies[target.Source.Key].Visible || _selected != null) throw new InvalidDataException("Filtered object remains pickable/selected.");
		_hud.Filters["exit"] = true; ApplyFilters();
		foreach (int elevation in new[] { 1, 2 })
		{
			ChangeElevation(elevation);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			int expected = _map.ObjectLayers.Single(l => l.Elevation == elevation).Objects.Count(o => o.Header.Tile >= 0);
			if (_layout!.Elevation != elevation || _lookup.Count != expected || _bodies.Count != expected)
				throw new InvalidDataException("Elevation switching lost objects or included inventory records.");
			GD.Print($"PREVIEW_LAYER_OK: elevation {elevation}; {_layout.Floors.Length} floor cells; {expected} source objects; {_layout.InventoryRecords} inventory records retained outside scene.");
		}
		GoToEntry();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (_elevation != 0 || _lookup.Count != 1842) throw new InvalidDataException("Return to entry failed.");
		float yaw = _yaw;
		using var clockwise = new InputEventKey { Pressed = true, PhysicalKeycode = Key.E };
		_UnhandledInput(clockwise);
		if (Math.Abs(_yaw - yaw - Mathf.Pi / 6) > .0001f) throw new InvalidDataException("Camera rotation input failed.");
		using var counterclockwise = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Q };
		_UnhandledInput(counterclockwise);
		MapLayoutWriter.Verify(_map);
		ClearSelection(); FocusEntry();
		GD.Print($"PREVIEW_TEST_OK: elevation 0; {_layout!.Floors.Length} floor cells; {_lookup.Count} source objects; metadata, transforms, camera picking/rotation, filters, elevation switching and source bytes verified.");
	}
	public override void _ExitTree() => _resources.Dispose();
	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) _ = Shutdown(0);
	}
	private async System.Threading.Tasks.Task Shutdown(int code)
	{
		if (_shuttingDown) return;
		_shuttingDown = true; SetProcess(false); SetProcessUnhandledInput(false);
		CloseExplosivePreparation();
		CloseUseClock();
		CloseBarrierEffectReview();
		// Finish node deletion and managed finalizers while the native engine is still alive.
		for (int i = GetChildCount() - 1; i >= 0; i--) GetChild(i).QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_architectureParts.Clear(); _architectureFloor = Array.Empty<MultiMeshInstance3D>(); _architecture = null; _kit = null; _environment = null; _sun = null;
		_combat = null; _combatLabels.Clear();
		_resources.Dispose(); _materials.Clear(); _meshes.Clear(); _shapes.Clear(); _bodies.Clear(); _lookup.Clear();
		_floorArtworkMaterials.Clear(); _floorArtworkMesh = null;
		_entityMeshes.Clear(); _entityMaterials.Clear(); _entityShapes.Clear(); _containerVisuals.Clear(); _worldLabels.Clear();
		_placedUseNodes.Clear(); _placedUseLayer = null; _placedUseMesh = null; _placedUseMaterial = null;
		_replacementNodes.Clear(); _replacementMesh = null; _replacementMaterial = null;
		GC.Collect(); GC.WaitForPendingFinalizers();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().Quit(code);
	}
}

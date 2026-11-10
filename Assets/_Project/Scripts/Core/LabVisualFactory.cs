using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MRLab.Joule
{
    /// <summary>Creates editable, metre-scale laboratory geometry with project-owned assets.</summary>
    public static class LabVisualFactory
    {
        private const string ModelDirectory = "Assets/_Project/Art/Models/GeneratedLab";
        private const string MaterialDirectory = "Assets/_Project/Art/Materials/GeneratedLab";
        private const string TextureDirectory = "Assets/_Project/Art/Textures/GeneratedLab";
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static TMP_FontAsset labelFont;
        private static Material white, slate, teal, steel, black, red, brass, glass, screen, lightMaterial;

        public static LabVisualController Create(Transform environment, Transform devices,
            Transform interactables, Transform effects, TMP_FontAsset font)
        {
            labelFont = font != null ? font : TMP_Settings.defaultFontAsset;
            Meshes.Clear();
            Materials.Clear();
            white = Surface("Lab_Porcelain_MAT", new Color(.8f, .85f, .85f), .05f, .5f);
            slate = Surface("Lab_Graphite_MAT", new Color(.095f, .15f, .18f), .25f, .32f);
            teal = Surface("Lab_Teal_MAT", new Color(.018f, .32f, .33f), .3f, .45f);
            steel = Surface("Lab_BrushedSteel_MAT", new Color(.48f, .58f, .59f), .7f, .65f);
            black = Surface("Lab_Rubber_MAT", new Color(.025f, .04f, .048f), 0f, .25f);
            red = Surface("Lab_CableRed_MAT", new Color(.64f, .065f, .045f), .05f, .4f);
            brass = Surface("Lab_Brass_MAT", new Color(.79f, .52f, .18f), .7f, .55f);
            glass = Surface("Lab_DisplayGlass_MAT", new Color(.04f, .11f, .13f), .2f, .88f);
            screen = Surface("Lab_Screen_MAT", new Color(.025f, .11f, .12f), 0f, .3f, new Color(.02f, .1f, .12f));
            lightMaterial = Surface("Lab_CeilingLight_MAT", new Color(.85f, .97f, 1f), 0f, .5f, new Color(.7f, .9f, 1f) * 2f);
            BuildRoom(environment);
            BuildBench(environment);

            Transform rig = Group("ExperimentBench", devices);
            var controller = rig.gameObject.AddComponent<LabVisualController>();
            Transform focus = Group("ExperimentFocus", rig);
            focus.localPosition = new Vector3(.05f, .99f, -.02f);
            Cube("HeatResistantTray", rig, new Vector3(.18f, .867f, .01f), new Vector3(1.22f, .045f, .64f), steel);
            Cube("CeramicInset", rig, new Vector3(.18f, .894f, .01f), new Vector3(1.14f, .013f, .55f), Surface("Lab_Ceramic_MAT", new Color(.62f, .7f, .68f), 0f, .2f));
            for (int n = 0; n < 11; n++)
                Cube("TrayScale_" + n, rig, new Vector3(-.34f + n * .1f, .902f, -.241f), new Vector3(.004f, .002f, n % 5 == 0 ? .026f : .014f), slate);
            Label("TrayCaption", rig, "耐热实验托盘  /  HEAT TEST ZONE", new Vector3(.2f, .905f, .26f), new Vector2(1.05f, .08f), .49f, new Color(.12f, .24f, .24f), new Vector3(90, 0, 0));
            Material acrylic = Surface("Lab_Acrylic_MAT", new Color(.53f, .78f, .80f, .16f), 0f, .95f);
            acrylic.SetFloat("_Surface", 1f);
            acrylic.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            acrylic.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            acrylic.SetFloat("_ZWrite", 0f);
            acrylic.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            acrylic.SetOverrideTag("RenderType", "Transparent");
            acrylic.renderQueue = (int)RenderQueue.Transparent;
            Cube("RearAcrylicShield", rig, new Vector3(.13f, 1.06f, .341f), new Vector3(1.12f, .32f, .006f), acrylic);
            foreach (float x in new[] { -.43f, .69f })
            {
                Cube("ShieldPost", rig, new Vector3(x, 1.065f, .342f), new Vector3(.014f, .36f, .014f), steel);
                Cube("ShieldFoot", rig, new Vector3(x, .864f, .34f), new Vector3(.064f, .026f, .064f), slate);
            }

            TMP_Text readout;
            Renderer powerLed;
            BuildSupply(rig, out readout, out powerLed);
            BuildLoad(rig);
            Renderer breakerLed;
            Transform lever;
            BuildBreaker(interactables, out breakerLed, out lever);
            BuildActions(interactables, rig);

            var allConductors = new List<Renderer>();
            allConductors.AddRange(Cable("SupplyPositive", rig, WirePathGeometry.Points(0), .014f, red));
            allConductors.AddRange(Cable("LoadPositive", rig, WirePathGeometry.Points(2), .014f, red));
            allConductors.AddRange(Cable("ReturnConductor", rig, WirePathGeometry.Points(3), .014f, black));
            Transform wireHolder = Group("TestWireHolder", rig);
            var testWire = Cable("HeatingWire", wireHolder, WirePathGeometry.Points(1), .018f, Surface("Lab_HotWire_MAT", new Color(.24f, .12f, .09f), .25f, .35f, new Color(.01f, 0f, 0f)));
            allConductors.AddRange(testWire);
            foreach (float x in new[] { -.22f, .24f })
            {
                Cube("CeramicTerminal", rig, new Vector3(x, .913f, -.13f), new Vector3(.071f, .032f, .085f), white);
                Cylinder("BrassTerminal", rig, new Vector3(x, .94f, -.13f), .032f, .025f, brass);
                Cube("TerminalSlot", rig, new Vector3(x, .954f, -.13f), new Vector3(.023f, .003f, .004f), slate);
            }
            var hotSpot = Sphere("WireHotspot", rig, new Vector3(.03f, .96f, -.1f), new Vector3(.05f, .032f, .038f), testWire[0].sharedMaterial);
            Transform jumper = Group("ShortCircuitJumper", rig);
            Cable("FaultJumper", jumper, new[] { Vector3.zero, new Vector3(.08f, .045f, -.025f), new Vector3(.11f, .0f, -.055f) }, .011f, brass);
            jumper.gameObject.SetActive(false);
            Transform selected = Group("SelectedFaultLocation", rig);
            Material markerMaterial = Surface("Lab_FaultMarker_MAT", new Color(1f, .65f, .05f), .1f, .4f, new Color(.7f, .32f, .015f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                Vector3 markerPosition = new Vector3(Mathf.Cos(angle) * .055f, .018f, Mathf.Sin(angle) * .055f);
                Cube("SelectionBracket_" + i, selected, markerPosition,
                    i % 2 == 0 ? new Vector3(.007f, .007f, .045f) : new Vector3(.045f, .007f, .007f), markerMaterial);
            }

            Transform sensor = Group("TemperatureSensor", rig);
            Cube("SensorBase", sensor, new Vector3(.21f, .925f, .145f), new Vector3(.30f, .065f, .11f), slate);
            Cube("SensorDisplay", sensor, new Vector3(.21f, .962f, .14f), new Vector3(.252f, .011f, .083f), screen);
            TMP_Text temperature = Label("TemperatureReading", sensor, "23.0 °C", new Vector3(.21f, .969f, .14f), new Vector2(.25f, .08f), .61f, new Color(.35f, 1f, .72f), new Vector3(90, 0, 0));
            Cable("Thermocouple", sensor, new[] { new Vector3(.09f, .93f, .13f), new Vector3(-.06f, .919f, .13f), new Vector3(-.04f, .934f, -.02f), new Vector3(.03f, .956f, -.085f) }, .005f, brass);

            Transform faultAction = Group("FaultInjectionHandle", interactables);
            Cube("FaultHandleBase", faultAction, new Vector3(.41f, .861f, -.50f), new Vector3(.34f, .036f, .14f), slate);
            Cube("FaultHandle", faultAction, new Vector3(.41f, .902f, -.50f), new Vector3(.12f, .05f, .07f), brass);
            Label("FaultLabel", faultAction, "故障接入", new Vector3(.41f, .894f, -.602f), new Vector2(.30f, .08f), .52f, new Color(.15f, .24f, .23f));
            Action(faultAction, LabAction.Fault, "切换短路故障", new Vector3(.41f, .90f, -.50f), new Vector3(.36f, .13f, .18f));
            Action(wireHolder, LabAction.Inspect, "观察导线与焦耳热", new Vector3(.03f, .97f, -.1f), new Vector3(.57f, .12f, .20f));

            Transform thermal = Group("ThermalOverlayMarker", effects);
            Label("ThermalCaption", thermal, "红外测温  ·  导线热点", new Vector3(.04f, 1.20f, -.12f), new Vector2(.8f, .12f), .68f, new Color(1f, .50f, .12f));
            thermal.gameObject.SetActive(false);
            Vector3 source = new Vector3(.03f, .985f, -.10f);
            ParticleSystem smoke = Smoke(effects, source);
            ParticleSystem flame = Flame(effects, source);
            ParticleSystem sparks = Sparks(effects, source);
            Transform lightTransform = Group("HotWireGlow", effects);
            lightTransform.localPosition = source + Vector3.up * .12f;
            Light glow = lightTransform.gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 1.8f;
            glow.intensity = 0f;
            glow.color = new Color(1f, .24f, .025f);
            glow.enabled = false;

            controller.Configure(focus, allConductors.ToArray(), powerLed, breakerLed, hotSpot, jumper.gameObject,
                lever, readout, temperature, smoke, flame, sparks, glow, thermal.gameObject, selected);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                AssetDatabase.SaveAssets();
#endif
            return controller;
        }

        private static void BuildRoom(Transform parent)
        {
            Transform room = Group("LaboratoryRoom", parent);
            Material floor = Surface("Lab_Floor_MAT", new Color(.34f, .42f, .43f), 0f, .42f);
            Material wall = Surface("Lab_Wall_MAT", new Color(.70f, .77f, .76f), 0f, .15f);
            Cube("Floor", room, new Vector3(0, -.055f, .4f), new Vector3(8f, .1f, 8f), floor);
            for (int i = -4; i <= 4; i++)
            {
                Cube("FloorSeamX_" + i, room, new Vector3(i, -.003f, .4f), new Vector3(.009f, .003f, 8), steel);
                Cube("FloorSeamZ_" + i, room, new Vector3(0, -.003f, i + .4f), new Vector3(8, .003f, .009f), steel);
            }
            Cube("BackWall", room, new Vector3(0, 1.65f, 2.7f), new Vector3(8, 3.3f, .16f), wall);
            Cube("LeftWall", room, new Vector3(-3.9f, 1.65f, .4f), new Vector3(.16f, 3.3f, 4.6f), wall);
            Cube("WallWainscot", room, new Vector3(0, .43f, 2.607f), new Vector3(8, .85f, .02f), teal);
            Cube("WallBand", room, new Vector3(0, .90f, 2.59f), new Vector3(8, .025f, .02f), brass);
            Cube("Skirting", room, new Vector3(0, .045f, 2.59f), new Vector3(8, .085f, .04f), slate);
            for (int i = 0; i < 5; i++)
            {
                float x = -2.85f + i * .75f;
                Transform cabinet = Group("RearCabinet_" + i, room);
                Cube("Carcass", cabinet, new Vector3(x, .42f, 2.2f), new Vector3(.72f, .82f, .68f), slate);
                Cube("Door", cabinet, new Vector3(x, .44f, 1.85f), new Vector3(.68f, .72f, .035f), white);
                Cube("Handle", cabinet, new Vector3(x + .23f, .65f, 1.82f), new Vector3(.13f, .025f, .032f), steel);
                Cube("ToeKick", cabinet, new Vector3(x, .055f, 1.93f), new Vector3(.7f, .09f, .48f), black);
            }
            Cube("RearWorktop", room, new Vector3(-1.34f, .855f, 2.18f), new Vector3(3.82f, .065f, .78f), steel);
            Cube("SafetyNotice", room, new Vector3(1.45f, 1.77f, 2.586f), new Vector3(1.4f, 1.18f, .024f), slate);
            Label("SafetyNoticeTitle", room, "ELECTRICAL SAFETY", new Vector3(1.45f, 2.14f, 2.565f), new Vector2(1.25f, .18f), .97f, new Color(.68f, .91f, .89f));
            Label("SafetyNoticeBody", room, "先断电  再处置\n\n异常升温 → 材料热解 → 起火\n\nLAB 10  /  焦耳定律实验", new Vector3(1.45f, 1.74f, 2.561f), new Vector2(1.2f, .60f), .76f, Color.white);
            Cube("UpperShelf", room, new Vector3(-1.32f, 1.77f, 2.35f), new Vector3(3.66f, .045f, .43f), steel);
            for (int i = 0; i < 3; i++)
            {
                float x = -2.6f + i * .7f;
                Cube("InstrumentCase_" + i, room, new Vector3(x, 1.935f, 2.34f), new Vector3(.38f, .27f, .25f), i % 2 == 0 ? teal : white);
                Cube("InstrumentHandle_" + i, room, new Vector3(x, 2.075f, 2.34f), new Vector3(.17f, .03f, .04f), black);
            }
            Cylinder("CableReel", room, new Vector3(-2.9f, 1.04f, 2.16f), .26f, .30f, black, new Vector3(90, 0, 0));
            Cube("Scope", room, new Vector3(-1.87f, 1.02f, 2.15f), new Vector3(.46f, .30f, .28f), white);
            Cube("ScopeScreen", room, new Vector3(-1.95f, 1.04f, 2.004f), new Vector3(.24f, .19f, .006f), screen);
            Label("ScopeWaveform", room, "~~~", new Vector3(-1.95f, 1.035f, 1.997f), new Vector2(.22f, .16f), 1.1f, new Color(.3f, .96f, .72f));
            Cube("WindowFrame", room, new Vector3(-3.797f, 1.96f, .7f), new Vector3(.06f, 1.55f, 2.25f), slate);
            Cube("WindowGlass", room, new Vector3(-3.76f, 1.96f, .7f), new Vector3(.03f, 1.45f, 2.13f), Surface("Lab_Window_MAT", new Color(.50f, .72f, .78f), .1f, .8f, new Color(.12f, .21f, .23f)));
            for (int i = 0; i < 3; i++)
                Cube("WindowMullion_" + i, room, new Vector3(-3.73f, 1.96f, -.02f + i * .72f), new Vector3(.035f, 1.45f, .03f), white);
            Cube("WindowHorizontal", room, new Vector3(-3.728f, 1.96f, .7f), new Vector3(.035f, .032f, 2.13f), white);
            Cube("CeilingFixture", room, new Vector3(-.2f, 3.05f, .7f), new Vector3(2.6f, .04f, .38f), lightMaterial);
            Cube("RearFixture", room, new Vector3(-1.4f, 2.9f, 2.13f), new Vector3(2.8f, .04f, .18f), lightMaterial);
        }

        private static void BuildBench(Transform parent)
        {
            Transform bench = Group("LaboratoryBench", parent);
            Cube("BenchTop", bench, new Vector3(0, .80f, 0), new Vector3(3.10f, .08f, 1.38f), white);
            Cube("FrontEdge", bench, new Vector3(0, .788f, -.697f), new Vector3(3.1f, .066f, .025f), slate);
            Cube("RearCableRail", bench, new Vector3(0, .87f, .59f), new Vector3(3f, .06f, .055f), steel);
            for (int n = 0; n < 4; n++)
            {
                float x = n < 2 ? -1.34f : 1.34f;
                float z = n % 2 == 0 ? -.48f : .48f;
                Cube("Leg_" + n, bench, new Vector3(x, .37f, z), new Vector3(.065f, .74f, .065f), steel);
                Cube("Foot_" + n, bench, new Vector3(x, .024f, z), new Vector3(.088f, .042f, .088f), black);
            }
            Cube("UnderBenchBrace", bench, new Vector3(0, .18f, .45f), new Vector3(2.7f, .055f, .05f), steel);
            Cube("DrawerBody", bench, new Vector3(-.97f, .60f, .04f), new Vector3(.62f, .30f, .93f), slate);
            Cube("DrawerFace", bench, new Vector3(-.97f, .62f, -.44f), new Vector3(.58f, .22f, .035f), teal);
            Cube("DrawerPull", bench, new Vector3(-.97f, .63f, -.475f), new Vector3(.24f, .028f, .032f), steel);
            Label("BenchTitle", bench, "JOULE LAB  /  10", new Vector3(.1f, .73f, -.715f), new Vector2(1.25f, .12f), .95f, new Color(.77f, .9f, .88f));
            Label("BenchSubtitle", bench, "电气热效应与短路起火", new Vector3(.05f, .856f, -.49f), new Vector2(.6f, .16f), .49f, new Color(.13f, .28f, .28f), new Vector3(90, 0, 0));
            for (int i = 0; i < 6; i++)
                Cube("CautionStripe_" + i, bench, new Vector3(.75f + i * .08f, .846f, -.63f), new Vector3(.036f, .003f, .074f), i % 2 == 0 ? brass : slate, new Vector3(0, 30, 0));
        }

        private static void BuildSupply(Transform parent, out TMP_Text readout, out Renderer led)
        {
            Transform supply = Group("DCPowerSupply", parent);
            Cube("Body", supply, new Vector3(-.99f, 1.041f, .15f), new Vector3(.48f, .38f, .40f), white);
            Cube("FrontPanel", supply, new Vector3(-.99f, 1.041f, -.058f), new Vector3(.452f, .345f, .019f), slate);
            Cube("DisplayBezel", supply, new Vector3(-1.045f, 1.105f, -.072f), new Vector3(.30f, .118f, .014f), black);
            Cube("DisplayGlass", supply, new Vector3(-1.045f, 1.105f, -.081f), new Vector3(.276f, .096f, .007f), screen);
            readout = Label("SupplyReadout", supply, "DC  0.00 A", new Vector3(-1.045f, 1.105f, -.088f), new Vector2(.264f, .084f), .57f, new Color(.39f, 1f, .77f));
            Label("SupplyName", supply, "DC POWER", new Vector3(-1.03f, 1.19f, -.073f), new Vector2(.30f, .045f), .38f, new Color(.81f, .88f, .87f));
            for (int i = 0; i < 2; i++)
            {
                Cylinder("AdjustmentDial_" + i, supply, new Vector3(-.819f, 1.10f - i * .087f, -.086f), .052f, .035f, steel, new Vector3(90, 0, 0));
                Cube("DialMark_" + i, supply, new Vector3(-.819f, 1.119f - i * .087f, -.106f), new Vector3(.003f, .012f, .003f), white);
            }
            for (int i = 0; i < 7; i++)
                Cube("Vent_" + i, supply, new Vector3(-1.236f, 1.03f, -.01f + i * .041f), new Vector3(.003f, .14f, .014f), black);
            foreach (Vector2 offset in new[] { new Vector2(-.195f, -.135f), new Vector2(.195f, -.135f), new Vector2(-.195f, .135f), new Vector2(.195f, .135f) })
                Cylinder("PanelScrew", supply, new Vector3(-.99f + offset.x, 1.041f + offset.y, -.072f), .012f, .005f, steel, new Vector3(90, 0, 0));
            Cylinder("PositiveSocket", supply, new Vector3(-.94f, .943f, -.08f), .035f, .025f, red, new Vector3(90, 0, 0));
            Cylinder("NegativeSocket", supply, new Vector3(-1.01f, .943f, -.08f), .035f, .025f, black, new Vector3(90, 0, 0));
            led = Sphere("PowerIndicator", supply, new Vector3(-1.156f, .945f, -.075f), new Vector3(.019f, .019f, .006f), Surface("Lab_PowerLED_MAT", new Color(.03f, .14f, .12f), 0f, .4f, new Color(.001f, .01f, .001f)));
            for (int i = 0; i < 2; i++)
                Cube("SupplyFoot_" + i, supply, new Vector3(-1.15f + i * .32f, .852f, .15f), new Vector3(.047f, .025f, .30f), black);
            Action(supply, LabAction.Power, "接通 / 断开实验电源", new Vector3(-.99f, 1.04f, .15f), new Vector3(.50f, .40f, .42f));
        }

        private static void BuildLoad(Transform parent)
        {
            Transform load = Group("LoadResistor", parent);
            Cube("InstrumentBase", load, new Vector3(.85f, .885f, .15f), new Vector3(.50f, .065f, .42f), slate);
            Cube("LoadHousing", load, new Vector3(.85f, 1.016f, .15f), new Vector3(.40f, .20f, .32f), teal);
            for (int i = 0; i < 9; i++)
                Cube("CoolingFin_" + i, load, new Vector3(.675f + i * .043f, 1.12f, .15f), new Vector3(.012f, .05f, .29f), steel);
            Cube("LoadNameplate", load, new Vector3(.85f, 1.023f, -.015f), new Vector3(.29f, .098f, .012f), white);
            Label("LoadLabel", load, "可调负载  R", new Vector3(.85f, 1.023f, -.024f), new Vector2(.28f, .09f), .57f, new Color(.04f, .21f, .22f));
            foreach (float x in new[] { .67f, 1.03f })
                Cylinder("LoadTerminal", load, new Vector3(x, .94f, -.046f), .022f, .036f, brass, new Vector3(90, 0, 0));
            Action(load, LabAction.Inspect, "观察负载：短路支路会绕过正常负载", new Vector3(.85f, 1.02f, .15f), new Vector3(.52f, .34f, .44f));
        }

        private static void BuildBreaker(Transform parent, out Renderer led, out Transform lever)
        {
            Transform breaker = Group("CircuitBreaker", parent);
            Cube("BreakerMount", breaker, new Vector3(-.49f, .872f, -.37f), new Vector3(.25f, .047f, .20f), steel);
            Cube("BreakerBody", breaker, new Vector3(-.49f, .97f, -.37f), new Vector3(.15f, .16f, .14f), white);
            Cube("BreakerLeverRecess", breaker, new Vector3(-.49f, .98f, -.446f), new Vector3(.075f, .085f, .01f), black);
            lever = Group("BreakerLever", breaker);
            lever.localPosition = new Vector3(-.49f, .977f, -.46f);
            Cube("LeverGrip", lever, new Vector3(0, 0, -.011f), new Vector3(.06f, .023f, .03f), teal);
            led = Sphere("BreakerIndicator", breaker, new Vector3(-.49f, 1.02f, -.449f), new Vector3(.024f, .009f, .005f), Surface("Lab_BreakerLED_MAT", new Color(.12f, .48f, .32f), 0f, .4f, new Color(.001f, .01f, .001f)));
            Label("BreakerLabel", breaker, "总断路器", new Vector3(-.49f, .861f, -.531f), new Vector2(.28f, .08f), .53f, new Color(.11f, .28f, .28f), new Vector3(90, 0, 0));
            Action(breaker, LabAction.Isolate, "断开总电源", new Vector3(-.49f, .96f, -.37f), new Vector3(.26f, .24f, .24f));
        }

        private static void BuildActions(Transform parent, Transform circuitFrame)
        {
            Transform alarm = Group("AlarmButton", parent);
            Cube("AlarmHousing", alarm, new Vector3(1.32f, 1.0f, .28f), new Vector3(.20f, .30f, .13f), red);
            Cube("AlarmWhiteFrame", alarm, new Vector3(1.32f, 1.015f, .208f), new Vector3(.14f, .15f, .012f), white);
            Cylinder("AlarmButtonFace", alarm, new Vector3(1.32f, 1.03f, .195f), .08f, .026f, red, new Vector3(90, 0, 0));
            Label("AlarmLabel", alarm, "报警", new Vector3(1.32f, .938f, .206f), new Vector2(.14f, .07f), .50f, Color.white);
            Action(alarm, LabAction.Alarm, "触发实验室火警", new Vector3(1.32f, 1f, .28f), new Vector3(.24f, .33f, .18f));

            Transform stand = Group("ExtinguisherStand", parent);
            Cube("ExtinguisherFloorMark", stand, new Vector3(1.81f, .007f, -.14f), new Vector3(.50f, .007f, .50f), teal);
            Label("FirePointLabel", stand, "FIRE POINT", new Vector3(1.81f, .013f, -.47f), new Vector2(.54f, .15f), .7f, new Color(.78f, .87f, .84f), new Vector3(90, 0, 0));
            Transform extinguisher = Group("CO2Extinguisher", parent);
            extinguisher.localPosition = new Vector3(1.81f, .43f, -.14f);
            Cylinder("CylinderBody", extinguisher, Vector3.zero, .22f, .57f, red);
            Sphere("CylinderShoulder", extinguisher, new Vector3(0f, .283f, 0f), new Vector3(.22f, .12f, .22f), red);
            Cylinder("Valve", extinguisher, new Vector3(0f, .37f, 0f), .051f, .10f, brass);
            Cube("CarryHandle", extinguisher, new Vector3(.03f, .425f, 0f), new Vector3(.16f, .034f, .047f), black);
            Transform triggerLever = Group("TriggerLever", extinguisher);
            triggerLever.localPosition = new Vector3(-.035f, .461f, 0f);
            triggerLever.localRotation = Quaternion.Euler(0, 0, -9);
            Cube("SafetyLever", triggerLever, new Vector3(.065f, 0f, 0f), new Vector3(.16f, .012f, .037f), steel);
            Transform horn = Group("HornAimPivot", extinguisher);
            horn.localPosition = new Vector3(-.18f, .18f, 0f);
            horn.localRotation = Quaternion.Euler(90f, 0, 0);
            Cylinder("Horn", horn, new Vector3(0f, 0f, .08f), .069f, .16f, black, new Vector3(90, 0, 0));
            Cylinder("HornMouth", horn, new Vector3(0f, 0f, .154f), .084f, .012f, black, new Vector3(90, 0, 0));
            Transform outlet = Group("SprayNozzle", horn);
            outlet.localPosition = new Vector3(0f, 0f, .165f);
            LineRenderer hoseLine = Line("FlexibleHose", extinguisher, black, .017f, 4);
            // Reuse a project-saved material: line vertex colours communicate aim validity.
            Material guideMaterial = ParticleMaterial(false);
            LineRenderer guide = Line("NozzleAimGuide", extinguisher, guideMaterial, .003f, 2);
            guide.enabled = false;
            Renderer aimMarker = Sphere("NozzleAimMarker", extinguisher, Vector3.zero, Vector3.one * .025f,
                Surface("Lab_AimMarker_MAT", new Color(1f, .65f, .12f), 0, .2f, new Color(.4f, .2f, .015f)));
            aimMarker.enabled = false;
            ParticleSystem spray = Particle("CO2Spray", outlet, Vector3.zero, new Color(.88f, .98f, 1f, .8f), .40f, .10f, 5.5f, 90f, false);
            spray.transform.localRotation = Quaternion.identity;
            var sprayMain = spray.main;
            sprayMain.maxParticles = 160;
            var sprayShape = spray.shape;
            sprayShape.angle = ExtinguisherInteraction.SprayHalfAngle;
            sprayShape.radius = .025f;
            var sprayNoise = spray.noise;
            sprayNoise.strength = .035f;
            var sprayFade = spray.colorOverLifetime;
            var sprayGradient = new Gradient();
            sprayGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.83f, .95f, 1f), 1f) },
                new[] { new GradientAlphaKey(.0f, 0f), new GradientAlphaKey(.65f, .08f), new GradientAlphaKey(0f, 1f) });
            sprayFade.color = new ParticleSystem.MinMaxGradient(sprayGradient);
            Cube("ExtinguisherLabel", extinguisher, new Vector3(0f, .05f, -.114f), new Vector3(.15f, .23f, .004f), white);
            Label("ExtinguisherLabelText", extinguisher, "CO<sub>2</sub>\n灭火器", new Vector3(0f, .05f, -.119f), new Vector2(.15f, .2f), .50f, new Color(.13f, .21f, .2f));
            Cylinder("FootRing", extinguisher, new Vector3(0f, -.285f, 0f), .23f, .025f, black);
            Action(extinguisher, LabAction.Extinguish, "抓取灭火器，瞄准后按住压把喷射", new Vector3(-.04f, .07f, 0f), new Vector3(.39f, .88f, .31f));
            var portableTool = extinguisher.gameObject.AddComponent<ExtinguisherInteraction>();
            portableTool.Configure(Object.FindObjectOfType<JouleExperimentManager>(), circuitFrame,
                horn, outlet, triggerLever, spray, hoseLine, guide, aimMarker);
        }

        private static LineRenderer Line(string name, Transform parent, Material material, float width, int count)
        {
            var line = Group(name, parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.positionCount = count;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Transform Group(string name, Transform parent)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        private static Renderer Cube(string name, Transform parent, Vector3 position, Vector3 dimensions, Material material, Vector3 rotation = default)
        {
            return Geometry(name, parent, position, dimensions, PrimitiveType.Cube, material, rotation);
        }

        private static Renderer Sphere(string name, Transform parent, Vector3 position, Vector3 dimensions, Material material)
        {
            return Geometry(name, parent, position, dimensions, PrimitiveType.Sphere, material, Vector3.zero);
        }

        private static Renderer Cylinder(string name, Transform parent, Vector3 position, float diameter, float height, Material material, Vector3 rotation = default)
        {
            return Geometry(name, parent, position, new Vector3(diameter, height * .5f, diameter), PrimitiveType.Cylinder, material, rotation);
        }

        private static Renderer Geometry(string name, Transform parent, Vector3 position, Vector3 dimensions, PrimitiveType type, Material material, Vector3 rotation)
        {
            Transform item = Group(name, parent);
            item.localPosition = position;
            item.localRotation = Quaternion.Euler(rotation);
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = BakedMesh(type, dimensions);
            var renderer = item.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return renderer;
        }

        private static Mesh BakedMesh(PrimitiveType type, Vector3 size)
        {
            // Shared generated geometry uses the Lab asset family prefix so its
            // model/material/texture names remain discoverable as one project set.
            string key = "Lab_" + type + "_" + Dimension(size.x) + "_" + Dimension(size.y) + "_" + Dimension(size.z);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;
#if UNITY_EDITOR
            string path = ModelDirectory + "/" + key + ".asset";
            if (!Application.isPlaying)
            {
                // Assignment 2 keeps the baseline asset intact. A later scene
                // regeneration should reuse the verified Blender result too.
                Mesh optimized = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Project/Art/Models/Assignment02/Optimized/" + key + ".asset");
                if (optimized != null)
                {
                    Meshes[key] = optimized;
                    return optimized;
                }
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null)
                {
                    string legacyPath = ModelDirectory + "/Mesh_" + key.Substring(4) + ".asset";
                    Mesh legacy = AssetDatabase.LoadAssetAtPath<Mesh>(legacyPath);
                    if (legacy != null)
                    {
                        string error = AssetDatabase.MoveAsset(legacyPath, path);
                        if (!string.IsNullOrEmpty(error))
                            throw new System.InvalidOperationException("Cannot normalize laboratory mesh name: " + error);
                        legacy.name = key;
                        EditorUtility.SetDirty(legacy);
                        saved = legacy;
                    }
                }
                if (saved != null)
                {
                    Meshes[key] = saved;
                    return saved;
                }
            }
#endif
            GameObject source = GameObject.CreatePrimitive(type);
            Mesh result = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
            result.name = key;
            var vertices = result.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector3.Scale(vertices[i], size);
            result.vertices = vertices;
            var normals = result.normals;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = new Vector3(normals[i].x / size.x, normals[i].y / size.y, normals[i].z / size.z).normalized;
            result.normals = normals;
            result.RecalculateBounds();
            if (Application.isPlaying)
                Object.Destroy(source);
            else
                Object.DestroyImmediate(source);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Folder(ModelDirectory);
                AssetDatabase.CreateAsset(result, path);
            }
#endif
            Meshes[key] = result;
            return result;
        }

        private static string Dimension(float dimension)
        {
            return dimension.ToString("0.00000", CultureInfo.InvariantCulture).Replace(".", "p").Replace("-", "m");
        }

        private static Renderer[] Cable(string name, Transform parent, Vector3[] points, float diameter, Material material)
        {
            Transform cable = Group(name, parent);
            var renderers = new List<Renderer>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                Renderer piece = Cylinder("Segment_" + i, cable, (points[i] + points[i + 1]) * .5f, diameter, delta.magnitude, material);
                piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
                renderers.Add(piece);
            }
            for (int i = 1; i < points.Length - 1; i++)
                renderers.Add(Sphere("Bend_" + i, cable, points[i], Vector3.one * diameter, material));
            return renderers.ToArray();
        }

        private static TMP_Text Label(string name, Transform parent, string content, Vector3 position,
            Vector2 size, float fontSize, Color color, Vector3 rotation = default)
        {
            Transform item = Group(name, parent);
            item.localPosition = position;
            item.localRotation = Quaternion.Euler(rotation);
            var text = item.gameObject.AddComponent<TextMeshPro>();
            text.font = labelFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = size;
            text.raycastTarget = false;
            return text;
        }

        private static void Action(Transform target, LabAction action, string label, Vector3 center, Vector3 size)
        {
            LabInteractable interaction = target.gameObject.AddComponent<LabInteractable>();
            interaction.Action = action;
            interaction.Label = label;
            var collider = target.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static Material Surface(string name, Color color, float metal, float smoothness, Color emission = default)
        {
            if (Materials.TryGetValue(name, out Material material) && material != null)
                return material;
#if UNITY_EDITOR
            string path = MaterialDirectory + "/" + name + ".mat";
            if (!Application.isPlaying)
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
#endif
            if (material == null)
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Folder(MaterialDirectory);
                if (!AssetDatabase.Contains(material))
                    AssetDatabase.CreateAsset(material, path);
                EditorUtility.SetDirty(material);
            }
#endif
            Materials[name] = material;
            return material;
        }

        private static ParticleSystem Particle(string name, Transform parent, Vector3 position, Color color, float lifetime, float size, float speed, float rate, bool fire)
        {
            Transform item = Group(name, parent);
            item.localPosition = position;
            item.localRotation = Quaternion.Euler(-90, 0, 0);
            var system = item.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size);
            main.startColor = color;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = fire ? 12f : 9f;
            shape.radius = fire ? .037f : .043f;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = fire ? .07f : .10f;
            noise.frequency = .8f;
            noise.scrollSpeed = .5f;
            var sizeLife = system.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.5f, fire ? 1.0f : 1.4f), new Keyframe(1f, fire ? .05f : 2.2f)));
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(fire ? new Color(1f, .92f, .26f) : Color.white, 0f), new GradientColorKey(fire ? new Color(1f, .24f, .03f) : new Color(.7f, .7f, .7f), .6f), new GradientColorKey(fire ? new Color(.6f, .025f, .001f) : new Color(.55f, .55f, .55f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(fire ? .9f : .5f, .15f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(fire);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static ParticleSystem Smoke(Transform parent, Vector3 position)
        {
            return Particle("WireSmoke", parent, position, new Color(.30f, .32f, .33f, .7f), 2.4f, .11f, .27f, 19f, false);
        }

        private static ParticleSystem Flame(Transform parent, Vector3 position)
        {
            return Particle("WireFlames", parent, position, Color.white, .65f, .10f, .38f, 36f, true);
        }

        private static ParticleSystem Sparks(Transform parent, Vector3 position)
        {
            ParticleSystem system = Particle("ShortCircuitSparks", parent, position, new Color(1f, .65f, .20f), .23f, .009f, .75f, 12f, true);
            var shape = system.shape;
            shape.angle = 67f;
            shape.radius = .018f;
            var main = system.main;
            main.gravityModifier = .25f;
            return system;
        }

        private static Material ParticleMaterial(bool fire)
        {
            string name = fire ? "Lab_Flame_MAT" : "Lab_Smoke_MAT";
            if (Materials.TryGetValue(name, out Material existing) && existing != null)
                return existing;
            Material material = null;
#if UNITY_EDITOR
            string path = MaterialDirectory + "/" + name + ".mat";
            if (!Application.isPlaying)
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
#endif
            if (material == null)
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            material.SetTexture("_BaseMap", ParticleTexture());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", fire ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", fire ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ColorMode", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Folder(MaterialDirectory);
                if (!AssetDatabase.Contains(material))
                    AssetDatabase.CreateAsset(material, path);
                EditorUtility.SetDirty(material);
            }
#endif
            Materials[name] = material;
            return material;
        }

        private static Texture2D ParticleTexture()
        {
            Texture2D texture = null;
#if UNITY_EDITOR
            string path = TextureDirectory + "/Lab_SoftParticle_Alpha.asset";
            if (!Application.isPlaying)
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#endif
            if (texture != null)
                return texture;
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Lab_SoftParticle_Alpha", wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.5f);
                    alpha *= Mathf.Lerp(.55f, 1f, Mathf.PerlinNoise(x * .14f, y * .14f));
                    colors[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(colors);
            texture.Apply();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Folder(TextureDirectory);
                AssetDatabase.CreateAsset(texture, path);
            }
#endif
            return texture;
        }

#if UNITY_EDITOR
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
#endif
    }
}

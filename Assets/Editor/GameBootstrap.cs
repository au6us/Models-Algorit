using System.IO;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ArenaSurvivor.Audio;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;
using ArenaSurvivor.Leveling;
using ArenaSurvivor.Managers;
using ArenaSurvivor.Player;
using ArenaSurvivor.Progression;
using ArenaSurvivor.UI;
using ArenaSurvivor.Weapons;
using Object = UnityEngine.Object;

namespace ArenaSurvivor.EditorTools
{
    /// <summary>
    /// Arma de punta a punta todo lo que falta del juego (Pasos 8 a 15): prefabs de
    /// proyectil y gema de XP, assets de datos, spawner de enemigos, spawner de XP,
    /// GameManager, y toda la UI (barra de XP, timer, contador de kills, panel de subida
    /// de nivel, panel de fin de partida). Vive en Assets/Editor, no entra en ningún build.
    ///
    /// Todo objeto que crea se arma DESACTIVADO, se cablea por completo (SerializedObject)
    /// y recién se reactiva al final — así ningún Awake() corre con referencias sin asignar.
    /// </summary>
    public static class GameBootstrap
    {
        // Un solo azul para el muro del Ground, el marco de pantalla y el fondo de cámara —
        // así los tres se funden en un único borde continuo en vez de leerse como piezas
        // separadas cuando la cámara llega a una esquina.
        private static readonly Color32 WindowFrameBlue = new Color32(0x08, 0x4C, 0xC7, 0xFF);

        // Alto de la barra de título — compartido entre BuildWindowTitleBar (la crea) y
        // BuildWindowFrameSides (reserva ese mismo espacio arriba del windowContainer) para
        // que nunca puedan quedar desincronizados.
        private const float TitleBarHeight = 34f;

        [MenuItem("Tools/Arena Survivor/Build Everything")]
        private static void BuildEverything()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró el Player en la escena.");
                return;
            }

            var circleSprite = player.GetComponent<SpriteRenderer>().sprite;

            // Se hace antes de cualquier otra cosa que toque estos sprites (BuildCursorController,
            // BuildPlayerCursorSkin): reimportar acá invalida referencias Sprite ya obtenidas, así
            // que el reajuste tiene que pasar primero, no después de que alguien ya los cargó.
            EnsureCursorSpritesScale();

            var enemyPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy.prefab");
            if (enemyPrefabAsset == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró Assets/Prefabs/Enemies/Enemy.prefab.");
                return;
            }

            EnsureFolder("Assets/Prefabs/Weapons");
            EnsureFolder("Assets/Prefabs/Progression");
            EnsureFolder("Assets/Prefabs/Fx");
            EnsureFolder("Assets/Data/Weapons");

            // ---- Data assets ----
            var weaponDataMagic = CreateWeaponData("Assets/Data/Weapons/WeaponData_MagicMissile.asset", "Bala Mágica",
                maxLevel: 5, baseDamage: 10f, damagePerLevel: 4f, baseCooldown: 1f, cooldownReductionPerLevel: 0.08f,
                minCooldown: 0.25f, projectileSpeed: 12f, projectileLifetime: 3f);

            var weaponDataOrbital = CreateWeaponData("Assets/Data/Weapons/WeaponData_Orbital.asset", "Orbe Espectral",
                maxLevel: 5, baseDamage: 8f, damagePerLevel: 3f, baseCooldown: 1f, cooldownReductionPerLevel: 0f,
                minCooldown: 1f, projectileSpeed: 1f, projectileLifetime: 1f);

            var weaponDataLaser = CreateWeaponData("Assets/Data/Weapons/WeaponData_Laser.asset", "Haz Perforante",
                maxLevel: 5, baseDamage: 6f, damagePerLevel: 3f, baseCooldown: 1.8f, cooldownReductionPerLevel: 0.1f,
                minCooldown: 0.55f, projectileSpeed: 0f, projectileLifetime: 0f);

            var weaponDataChain = CreateWeaponData("Assets/Data/Weapons/WeaponData_Chain.asset", "Cadena Eléctrica",
                maxLevel: 5, baseDamage: 7f, damagePerLevel: 3f, baseCooldown: 1.4f, cooldownReductionPerLevel: 0.08f,
                minCooldown: 0.45f, projectileSpeed: 0f, projectileLifetime: 0f);

            var weaponDataDrone = CreateWeaponData("Assets/Data/Weapons/WeaponData_Drone.asset", "Dron Acompañante",
                maxLevel: 5, baseDamage: 6f, damagePerLevel: 2f, baseCooldown: 1.2f, cooldownReductionPerLevel: 0.05f,
                minCooldown: 0.5f, projectileSpeed: 10f, projectileLifetime: 3f);

            // El cooldown del Haz y la Cadena se retocó después de probarlos (salían medio
            // seguido) — CreateWeaponData no pisa un asset que ya existe, así que esto se
            // reaplica siempre para que la partida ya armada también reciba el ajuste.
            SetField(weaponDataLaser, "baseCooldown", 1.8f);
            SetField(weaponDataLaser, "minCooldown", 0.55f);
            SetField(weaponDataChain, "baseCooldown", 1.4f);
            SetField(weaponDataChain, "minCooldown", 0.45f);

            var enemyDataBasic = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/EnemyData_Basic.asset");
            var enemyDataSwarm = CreateEnemyData("Assets/Data/Enemies/EnemyData_Swarm.asset",
                maxHealth: 8f, moveSpeed: 3.5f, contactDamage: 6f, experienceReward: 3,
                color: new Color(1f, 0.6f, 0.1f), scale: 0.7f);
            var enemyDataTank = CreateEnemyData("Assets/Data/Enemies/EnemyData_Tank.asset",
                maxHealth: 60f, moveSpeed: 1.1f, contactDamage: 18f, experienceReward: 12,
                color: new Color(0.4f, 0.05f, 0.05f), scale: 1.6f);
            var enemyDataElite = CreateEnemyData("Assets/Data/Enemies/EnemyData_Elite.asset",
                maxHealth: 140f, moveSpeed: 1.6f, contactDamage: 22f, experienceReward: 40,
                color: new Color(1f, 0.85f, 0.1f), scale: 2f, isSpecial: true);
            var enemyDataBoss = CreateEnemyData("Assets/Data/Enemies/EnemyData_Boss.asset",
                maxHealth: 900f, moveSpeed: 0.9f, contactDamage: 30f, experienceReward: 150,
                color: new Color(0.6f, 0.05f, 0.7f), scale: 3f, isSpecial: true, isBoss: true,
                displayName: "Virus.exe");

            // ---- Prefabs ----
            var projectilePrefab = BuildProjectilePrefab(circleSprite);
            EnsureProjectileTrail(projectilePrefab.gameObject);
            var xpGemPrefab = BuildXpGemPrefab(circleSprite);
            var chestPrefab = BuildChestPrefab(circleSprite);

            // ---- Prefabs de feedback (Fx) ----
            var damageNumberPrefab = BuildDamageNumberPrefab();
            var enemyDeathBurstPrefab = BuildParticleBurstPrefab("EnemyDeathBurst", "Assets/Prefabs/Fx/EnemyDeathBurst.prefab",
                burstCount: 16, startSpeed: 3.5f, startSize: 0.18f, startLifetime: 0.4f, radius: 0.1f);
            var impactBurstPrefab = BuildParticleBurstPrefab("ImpactBurst", "Assets/Prefabs/Fx/ImpactBurst.prefab",
                burstCount: 6, startSpeed: 2.5f, startSize: 0.1f, startLifetime: 0.2f, radius: 0.03f);
            var eliteKillRipplePrefab = BuildParticleBurstPrefab("EliteKillRipple", "Assets/Prefabs/Fx/EliteKillRipple.prefab",
                burstCount: 1, startSpeed: 0f, startSize: 1.5f, startLifetime: 0.4f, radius: 0f, growing: true);

            // ---- Player: stats, XP, armas ----
            player.SetActive(false);

            var playerStats = EnsureComponent<PlayerStats>(player);
            var playerLeveling = EnsureComponent<PlayerLeveling>(player);
            var playerHealth = player.GetComponent<Health>();

            var projectileWeapon = EnsureComponent<ProjectileWeapon>(player);
            SetField(projectileWeapon, "data", weaponDataMagic);
            SetField(projectileWeapon, "projectilePrefab", projectilePrefab);

            var orbitVisual = BuildOrbitVisual(player.transform, circleSprite);
            var orbitalWeapon = EnsureComponent<OrbitalWeapon>(player);
            SetField(orbitalWeapon, "data", weaponDataOrbital);
            SetField(orbitalWeapon, "orbitVisual", orbitVisual);
            orbitalWeapon.enabled = false;
            orbitVisual.gameObject.SetActive(false); // no se ve hasta desbloquearla (OnDisable ya lo hace, esto es cinturón y tiradores)

            var laserBeamVisual = BuildLineVisual(player.transform, "LaserBeamVisual", new Color(0.4f, 1f, 1f), 0.12f);
            var laserWeapon = EnsureComponent<LaserWeapon>(player);
            SetField(laserWeapon, "data", weaponDataLaser);
            SetField(laserWeapon, "beamVisual", laserBeamVisual);
            laserWeapon.enabled = false;

            var chainVisual = BuildLineVisual(player.transform, "ChainVisual", new Color(0.6f, 0.8f, 1f), 0.08f);
            var chainWeapon = EnsureComponent<ChainLightningWeapon>(player);
            SetField(chainWeapon, "data", weaponDataChain);
            SetField(chainWeapon, "chainVisual", chainVisual);
            chainWeapon.enabled = false;

            var droneVisual = BuildDroneVisual(player.transform, circleSprite);
            var droneWeapon = EnsureComponent<DroneWeapon>(player);
            SetField(droneWeapon, "data", weaponDataDrone);
            SetField(droneWeapon, "projectilePrefab", projectilePrefab);
            SetField(droneWeapon, "droneVisual", droneVisual);
            droneWeapon.enabled = false;
            droneVisual.gameObject.SetActive(false); // no se ve hasta desbloquearlo (OnDisable ya lo hace, esto es cinturón y tiradores)

            // ---- Feedback de combate (knockback ya viene con default de fábrica en cada
            // script; acá solo lo que necesita wiring: flash, número de daño, partículas) ----
            EnsureComponent<HitFlash>(player);
            var playerDamageNumbers = EnsureComponent<DamageNumberSpawner>(player);
            SetField(playerDamageNumbers, "color", new Color(1f, 0.3f, 0.3f));

            // El dash arranca bloqueado y se desbloquea solo (PlayerLeveledUpEvent) — no hay
            // nada que cablearle desde acá, solo asegurarse de que el componente exista.
            var playerDash = EnsureComponent<PlayerDash>(player);

            ApplyCombatFeelToPrefabs(enemyPrefabAsset, xpGemPrefab);
            WireCameraFeedback();
            BuildFxHub(damageNumberPrefab, enemyDeathBurstPrefab, impactBurstPrefab, eliteKillRipplePrefab);
            BuildAudioManager();

            // ---- Enemy spawner (reemplaza al DebugEnemySpawnTester) ----
            RemoveGameObjectIfPresent("EnemySpawnTester");

            var spawnerObj = GameObject.Find("EnemySpawner");
            EnemySpawner spawner;
            if (spawnerObj == null)
            {
                var spawnerGO = new GameObject("EnemySpawner");
                spawnerGO.SetActive(false);
                spawner = spawnerGO.AddComponent<EnemySpawner>();
                SetField(spawner, "enemyPrefab", enemyPrefabAsset.GetComponent<Enemy>());
                SetField(spawner, "target", player.transform);
                SetWaveEntries(spawner, "waveEntries",
                    (enemyDataBasic, 0f),
                    (enemyDataSwarm, 20f),
                    (enemyDataTank, 45f));
                spawnerGO.SetActive(true);
            }
            else
            {
                spawner = spawnerObj.GetComponent<EnemySpawner>();
            }

            // Se reaplican siempre (no solo al crear el spawner): son arrays nuevos que una
            // escena guardada de antes de esto no tiene todavía, aunque el spawner ya exista.
            SetWaveEntries(spawner, "eliteWaveEntries",
                (enemyDataElite, 90f));
            SetBossSpawns(spawner, "bossSpawns",
                (enemyDataBoss, 120f));
            SetBurstSpawns(spawner, "burstSpawns",
                (60f, 15),
                (150f, 20));

            // ---- XP gem spawner ----
            if (GameObject.Find("XpGemSpawner") == null)
            {
                var xpSpawnerGO = new GameObject("XpGemSpawner");
                xpSpawnerGO.SetActive(false);
                var xpSpawner = xpSpawnerGO.AddComponent<XpGemSpawner>();
                SetField(xpSpawner, "gemPrefab", xpGemPrefab);
                SetField(xpSpawner, "player", player.transform);
                xpSpawnerGO.SetActive(true);
            }

            // ---- Cofres (los sueltan los enemigos especiales) ----
            if (GameObject.Find("ChestSpawner") == null)
            {
                var chestSpawnerGO = new GameObject("ChestSpawner");
                chestSpawnerGO.SetActive(false);
                var chestSpawner = chestSpawnerGO.AddComponent<ChestSpawner>();
                SetField(chestSpawner, "chestPrefab", chestPrefab);
                chestSpawnerGO.SetActive(true);
            }

            // ---- GameManager ----
            var gameManagerGO = GameObject.Find("GameManager");
            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.SetActive(false);
                gameManagerGO.AddComponent<GameManager>();
                gameManagerGO.SetActive(true);
            }

            // PauseController es nuevo — se suma también a un GameManager armado en una
            // corrida anterior de este método, no solo a uno recién creado.
            EnsureComponent<PauseController>(gameManagerGO);

            // ---- UI ----
            var canvasGO = GameObject.Find("Canvas");
            if (canvasGO == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró el Canvas en la escena.");
                player.SetActive(true);
                return;
            }

            var canvas = canvasGO.transform;
            var flatSprite = CreateFlatSpriteAsset();

            BuildXpBar(canvas, flatSprite);
            BuildTimerText(canvas);
            BuildKillCounterText(canvas);
            BuildDamageVignette(canvas, flatSprite);
            BuildLevelUpPanel(canvas, flatSprite, projectileWeapon, orbitalWeapon, playerStats, playerHealth);
            SetObjectArray(canvas.GetComponent<LevelUpManager>(), "weapons",
                new Object[] { projectileWeapon, orbitalWeapon, laserWeapon, chainWeapon, droneWeapon });
            BuildGameEndPanel(canvas);
            EnsureHealthBarHpText(canvas);
            BuildPlayerStatsText(canvas);
            BuildWeaponsHudText(canvas);
            BuildKillStreakText(canvas, flatSprite);
            BuildWaveWarningText(canvas);
            BuildBossHealthBar(canvas, flatSprite);
            BuildBossRecycleBin(canvas);
            BuildBsod(canvas);
            BuildThreatIndicator(canvas, enemyPrefabAsset);
            BuildWindowFrameSides(canvas, flatSprite);
            BuildWindowTitleBar(canvas);
            BuildCursorController(canvas);
            BuildPlayerCursorSkin(player, playerHealth, playerDash);
            EnsureHealthBarFillConfigured(canvas, flatSprite);
            EnsureLevelUpButtonSprites(canvas, flatSprite);

            ApplyBalanceTuning(playerHealth, playerLeveling, xpGemPrefab, spawner);
            ApplyPhysicsSmoothing(player, enemyPrefabAsset, projectilePrefab, xpGemPrefab);
            SetField(spawner, "groundTransform", GameObject.Find("Ground")?.transform);
            EnsureGroundPattern(flatSprite);
            BuildDynamicWindowFrame(canvas);
            BuildCrtScanlineEffect();

            player.SetActive(true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[GameBootstrap] Listo: spawner, armas, XP/nivel, panel de mejoras, HUD y fin de partida armados.");
        }

        // ---------------- Data assets ----------------

        private static WeaponData CreateWeaponData(string path, string weaponName, int maxLevel, float baseDamage,
            float damagePerLevel, float baseCooldown, float cooldownReductionPerLevel, float minCooldown,
            float projectileSpeed, float projectileLifetime)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(asset, path);

            SetField(asset, "weaponName", weaponName);
            SetField(asset, "maxLevel", maxLevel);
            SetField(asset, "baseDamage", baseDamage);
            SetField(asset, "damagePerLevel", damagePerLevel);
            SetField(asset, "baseCooldown", baseCooldown);
            SetField(asset, "cooldownReductionPerLevel", cooldownReductionPerLevel);
            SetField(asset, "minCooldown", minCooldown);
            SetField(asset, "projectileSpeed", projectileSpeed);
            SetField(asset, "projectileLifetime", projectileLifetime);

            return asset;
        }

        private static EnemyData CreateEnemyData(string path, float maxHealth, float moveSpeed, float contactDamage,
            int experienceReward, Color color, float scale, bool isSpecial = false, bool isBoss = false,
            string displayName = "Jefe")
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(asset, path);

            SetField(asset, "maxHealth", maxHealth);
            SetField(asset, "moveSpeed", moveSpeed);
            SetField(asset, "contactDamage", contactDamage);
            SetField(asset, "experienceReward", experienceReward);
            SetField(asset, "color", color);
            SetField(asset, "scale", scale);
            SetField(asset, "isSpecial", isSpecial);
            SetField(asset, "isBoss", isBoss);
            SetField(asset, "displayName", displayName);

            return asset;
        }

        // ---------------- Prefabs ----------------

        private static Projectile BuildProjectilePrefab(Sprite circleSprite)
        {
            const string path = "Assets/Prefabs/Weapons/Projectile.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Projectile>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(1f, 0.9f, 0.2f);
            go.transform.localScale = Vector3.one * 0.3f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<Projectile>();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return savedPrefab.GetComponent<Projectile>();
        }

        private static XpGem BuildXpGemPrefab(Sprite circleSprite)
        {
            const string path = "Assets/Prefabs/Progression/XpGem.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<XpGem>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("XpGem");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(0.2f, 0.9f, 1f);
            go.transform.localScale = Vector3.one * 0.25f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<XpGem>();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return savedPrefab.GetComponent<XpGem>();
        }

        private static Chest BuildChestPrefab(Sprite circleSprite)
        {
            const string path = "Assets/Prefabs/Progression/Chest.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Chest>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("Chest");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(1f, 0.85f, 0.1f);
            go.transform.localScale = Vector3.one * 0.5f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<Chest>();
            go.AddComponent<SquashStretch>();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return savedPrefab.GetComponent<Chest>();
        }

        /// <summary>Estela corta en la bala — puro brillo visual, no participa del impacto.</summary>
        private static void EnsureProjectileTrail(GameObject projectileGO)
        {
            var trail = EnsureComponent<TrailRenderer>(projectileGO);
            trail.time = 0.12f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.9f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            EditorUtility.SetDirty(projectileGO);
        }

        private static Transform BuildOrbitVisual(Transform playerTransform, Sprite circleSprite)
        {
            var existing = playerTransform.Find("OrbitVisual");
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("OrbitVisual");
            go.transform.SetParent(playerTransform, false);
            go.transform.localScale = Vector3.one * 0.4f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(0.7f, 0.3f, 1f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<OrbitalHitbox>();

            return go.transform;
        }

        private static LineRenderer BuildLineVisual(Transform playerTransform, string name, Color color, float width)
        {
            var existing = playerTransform.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<LineRenderer>();
            }

            var go = new GameObject(name);
            go.transform.SetParent(playerTransform, false);

            var lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;

            return lineRenderer;
        }

        private static Transform BuildDroneVisual(Transform playerTransform, Sprite circleSprite)
        {
            var existing = playerTransform.Find("DroneVisual");
            if (existing != null)
            {
                // DroneBob se renombró a DroneMovement (ahora también gira hacia el enemigo
                // más cercano) — limpia el componente huérfano de la clase vieja si quedó.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(existing.gameObject);
                EnsureComponent<DroneMovement>(existing.gameObject);
                return existing;
            }

            var go = new GameObject("DroneVisual");
            go.transform.SetParent(playerTransform, false);
            go.transform.localScale = Vector3.one * 0.35f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(0.2f, 1f, 0.6f);

            go.AddComponent<DroneMovement>();

            return go.transform;
        }

        // ---------------- Feedback (Fx) ----------------

        private static FloatingDamageNumber BuildDamageNumberPrefab()
        {
            const string path = "Assets/Prefabs/Fx/DamageNumber.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<FloatingDamageNumber>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("DamageNumber");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.font = font;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            var meshRenderer = EnsureComponent<MeshRenderer>(go);
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.sortingOrder = 20;

            go.AddComponent<FloatingDamageNumber>();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return savedPrefab.GetComponent<FloatingDamageNumber>();
        }

        private static PooledParticleEffect BuildParticleBurstPrefab(string name, string path, int burstCount,
            float startSpeed, float startSize, float startLifetime, float radius, bool growing = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PooledParticleEffect>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.5f;
            main.startLifetime = startLifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            // La mayoría de los efectos arrancan grandes y se achican (chispas de impacto,
            // muerte); el ripple de elite/jefe es al revés — un pulso que CRECE y se
            // desvanece, como una onda expansiva.
            sizeOverLifetime.size = growing
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 1f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 15;
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            go.AddComponent<PooledParticleEffect>();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return savedPrefab.GetComponent<PooledParticleEffect>();
        }

        /// <summary>Agrega flash/pop/número de daño a los prefabs de Enemy y XpGem (idempotente).</summary>
        private static void ApplyCombatFeelToPrefabs(GameObject enemyPrefabAsset, XpGem xpGemPrefab)
        {
            EnsureComponent<HitFlash>(enemyPrefabAsset);
            EnsureComponent<SquashStretch>(enemyPrefabAsset);
            var enemyDamageNumbers = EnsureComponent<DamageNumberSpawner>(enemyPrefabAsset);
            SetField(enemyDamageNumbers, "color", Color.white);
            EditorUtility.SetDirty(enemyPrefabAsset);

            EnsureComponent<SquashStretch>(xpGemPrefab.gameObject);
            EditorUtility.SetDirty(xpGemPrefab.gameObject);
        }

        /// <summary>Sacudida de cámara (Cinemachine Impulse) y zoom-punch al subir de nivel.</summary>
        private static void WireCameraFeedback()
        {
            var cameraGO = GameObject.Find("CinemachineCamera");
            if (cameraGO == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró CinemachineCamera para el feedback de cámara.");
                return;
            }

            // LevelUpCameraPunch se renombró a LevelUpCameraFocus (ahora sostiene el zoom en
            // vez de un golpecito) — esto limpia el componente huérfano que haya quedado de
            // la clase vieja en escenas guardadas antes del cambio.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cameraGO);

            EnsureComponent<CinemachineImpulseSource>(cameraGO);
            EnsureComponent<CinemachineImpulseListener>(cameraGO);
            EnsureComponent<CameraShakeController>(cameraGO);
            EnsureComponent<LevelUpCameraFocus>(cameraGO);

            var positionComposer = cameraGO.GetComponent<CinemachinePositionComposer>();
            if (positionComposer == null)
            {
                Debug.LogWarning("[GameBootstrap] CinemachineCamera no tiene un Position Composer — " +
                                 "el seguimiento de mouse en el panel de nivel no va a hacer nada.");
            }
        }

        /// <summary>Objeto único que agrupa los controllers globales de feedback (todos oyentes del EventManager).</summary>
        private static void BuildFxHub(FloatingDamageNumber damageNumberPrefab, PooledParticleEffect enemyDeathBurstPrefab,
            PooledParticleEffect impactBurstPrefab, PooledParticleEffect eliteKillRipplePrefab)
        {
            var fxHubGO = GameObject.Find("FxHub");
            var isNew = fxHubGO == null;
            if (isNew)
            {
                fxHubGO = new GameObject("FxHub");
            }

            fxHubGO.SetActive(false);

            EnsureComponent<HitStopController>(fxHubGO);

            var damageNumbers = EnsureComponent<DamageNumberController>(fxHubGO);
            SetField(damageNumbers, "prefab", damageNumberPrefab);

            var deathParticles = EnsureComponent<EnemyDeathParticles>(fxHubGO);
            SetField(deathParticles, "prefab", enemyDeathBurstPrefab);

            var impactParticles = EnsureComponent<WeaponImpactParticles>(fxHubGO);
            SetField(impactParticles, "prefab", impactBurstPrefab);
            SetField(impactParticles, "color", new Color(1f, 0.95f, 0.4f));

            // Nuevo: el pulso de elite/jefe se suma también a una FxHub ya existente de una
            // escena guardada de antes de este cambio, no solo a una creada de cero.
            var eliteRipple = EnsureComponent<EliteKillRipple>(fxHubGO);
            SetField(eliteRipple, "prefab", eliteKillRipplePrefab);

            fxHubGO.SetActive(true);
        }

        /// <summary>Música en loop + SFX puntuales — dos AudioSource separados (uno por objeto), ambos oyentes del EventManager.</summary>
        private static void BuildAudioManager()
        {
            var audioManagerGO = GameObject.Find("AudioManager");
            var isNew = audioManagerGO == null;
            if (isNew)
            {
                audioManagerGO = new GameObject("AudioManager");
            }

            audioManagerGO.SetActive(false);

            // Un solo lugar para calibrar todos los volúmenes (master + música + cada
            // efecto) sin tener que ir a buscar cada AudioSource por separado.
            var settings = EnsureComponent<AudioVolumeSettings>(audioManagerGO);

            var musicTransform = audioManagerGO.transform.Find("Music");
            GameObject musicGO;
            if (musicTransform == null)
            {
                musicGO = new GameObject("Music");
                musicGO.transform.SetParent(audioManagerGO.transform, false);
                musicGO.AddComponent<AudioSource>();
                musicGO.AddComponent<AudioLowPassFilter>();
            }
            else
            {
                musicGO = musicTransform.gameObject;
            }

            var musicPlayer = EnsureComponent<MusicPlayer>(musicGO);
            SetField(musicPlayer, "settings", settings);
            SetField(musicPlayer, "musicClip", LoadClip("Assets/SFX/OST.mp3"));

            var sfxTransform = audioManagerGO.transform.Find("Sfx");
            GameObject sfxGO;
            if (sfxTransform == null)
            {
                sfxGO = new GameObject("Sfx");
                sfxGO.transform.SetParent(audioManagerGO.transform, false);
                sfxGO.AddComponent<AudioSource>();
            }
            else
            {
                sfxGO = sfxTransform.gameObject;
            }

            var sfxPlayer = EnsureComponent<SfxPlayer>(sfxGO);
            SetField(sfxPlayer, "settings", settings);
            SetField(sfxPlayer, "shootClip", LoadClip("Assets/SFX/Shoot.mp3"));
            SetField(sfxPlayer, "impactClip", LoadClip("Assets/SFX/Impact.wav"));
            SetField(sfxPlayer, "enemyDeathClip", LoadClip("Assets/SFX/EnemyDeath.wav"));
            SetField(sfxPlayer, "playerHurtClip", LoadClip("Assets/SFX/HitMe.wav"));
            SetField(sfxPlayer, "xpClip", LoadClip("Assets/SFX/XP.wav"));
            SetField(sfxPlayer, "levelUpClip", LoadClip("Assets/SFX/LevelUp.wav"));
            SetField(sfxPlayer, "clickClip", LoadClip("Assets/SFX/Click.wav"));
            SetField(sfxPlayer, "bossDefeatedClip", LoadClip("Assets/Sprites/WinXp/Sounds/Windows XP Recycle.wav"));

            audioManagerGO.SetActive(true);
        }

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[GameBootstrap] No se encontró el audio en {path}.");
            }

            return clip;
        }

        private static void BuildDamageVignette(Transform canvas, Sprite uiSprite)
        {
            var existing = canvas.Find("DamageVignette");
            if (existing != null)
            {
                // El pulso de vida crítica se retoca de vez en cuando (intensidad) — se
                // reaplica siempre, no solo al crear el objeto la primera vez.
                RetuneCriticalPulse(existing.GetComponent<DamageVignetteUI>());
                return;
            }

            var go = new GameObject("DamageVignette", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            go.transform.SetAsFirstSibling(); // detrás de toda la demás UI (paneles, botones)

            var image = go.AddComponent<Image>();
            image.sprite = uiSprite;
            image.color = new Color(1f, 0f, 0f, 0f);
            image.raycastTarget = false;
            StretchFill(image.rectTransform);

            var vignetteUI = go.AddComponent<DamageVignetteUI>();
            SetField(vignetteUI, "vignetteImage", image);
            RetuneCriticalPulse(vignetteUI);
        }

        private static void RetuneCriticalPulse(DamageVignetteUI vignetteUI)
        {
            SetField(vignetteUI, "criticalPulseMinAlpha", 0.05f);
            SetField(vignetteUI, "criticalPulseMaxAlpha", 0.18f);
        }

        // ---------------- UI ----------------

        private static void BuildXpBar(Transform canvas, Sprite uiSprite)
        {
            if (canvas.Find("XpBar_Background") != null)
            {
                return;
            }

            var backgroundGO = new GameObject("XpBar_Background", typeof(RectTransform));
            backgroundGO.SetActive(false);
            backgroundGO.transform.SetParent(canvas, false);

            var background = backgroundGO.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);
            SetRect(background.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -110), new Vector2(500, 26));

            var fill = CreateImage("XpBar_Fill", background.transform, new Color(0.3f, 0.6f, 1f), uiSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            StretchFill(fill.rectTransform);

            var levelText = CreateText("XpBar_LevelText", canvas, "Nv. 1", 26, Color.white, TextAnchor.MiddleLeft);
            SetRect(levelText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(550, -108), new Vector2(150, 30));

            var xpBarUI = backgroundGO.AddComponent<XpBarUI>();
            SetField(xpBarUI, "fillImage", fill);
            SetField(xpBarUI, "levelText", levelText);

            backgroundGO.SetActive(true);
        }

        private static void BuildTimerText(Transform canvas)
        {
            if (canvas.Find("Timer_Text") != null)
            {
                return;
            }

            var timerGO = new GameObject("Timer_Text", typeof(RectTransform));
            timerGO.SetActive(false);
            timerGO.transform.SetParent(canvas, false);

            var timerText = timerGO.AddComponent<Text>();
            ConfigureText(timerText, "00:00", 40, Color.white, TextAnchor.MiddleCenter);
            SetRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -40), new Vector2(220, 60));

            var timerUI = timerGO.AddComponent<GameTimerUI>();
            SetField(timerUI, "timerText", timerText);

            timerGO.SetActive(true);
        }

        private static void BuildKillCounterText(Transform canvas)
        {
            if (canvas.Find("KillCounter_Text") != null)
            {
                return;
            }

            var killGO = new GameObject("KillCounter_Text", typeof(RectTransform));
            killGO.SetActive(false);
            killGO.transform.SetParent(canvas, false);

            var killText = killGO.AddComponent<Text>();
            ConfigureText(killText, "Kills: 0", 28, Color.white, TextAnchor.MiddleRight);
            SetRect(killText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-40, -40), new Vector2(260, 50));

            var killUI = killGO.AddComponent<KillCounterUI>();
            SetField(killUI, "countText", killText);

            killGO.SetActive(true);
        }

        private static void BuildLevelUpPanel(Transform canvas, Sprite uiSprite, ProjectileWeapon projectileWeapon,
            OrbitalWeapon orbitalWeapon, PlayerStats playerStats, Health playerHealth)
        {
            var panelTransform = canvas.Find("LevelUpPanel");
            GameObject panelGO;
            var buttons = new Object[3];

            if (panelTransform == null)
            {
                panelGO = new GameObject("LevelUpPanel", typeof(RectTransform));
                panelGO.SetActive(false);
                panelGO.transform.SetParent(canvas, false);

                var dim = panelGO.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.75f);
                StretchFill(dim.rectTransform);

                var title = CreateText("Title", panelGO.transform, "¡Subiste de nivel!", 48, Color.white, TextAnchor.MiddleCenter);
                SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 220), new Vector2(800, 80));

                for (var i = 0; i < 3; i++)
                {
                    var button = CreateButton($"Option_{i}", panelGO.transform, "Opción", uiSprite);
                    SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0, 60 - i * 130), new Vector2(600, 100));

                    var buttonUI = button.gameObject.AddComponent<LevelUpButtonUI>();
                    SetField(buttonUI, "button", button);
                    SetField(buttonUI, "titleText", button.GetComponentInChildren<Text>());
                    button.gameObject.AddComponent<CursorHoverHint>();
                    buttons[i] = buttonUI;
                }
            }
            else
            {
                panelGO = panelTransform.gameObject;
                for (var i = 0; i < 3; i++)
                {
                    var buttonTransform = panelTransform.Find($"Option_{i}");
                    buttons[i] = buttonTransform.GetComponent<LevelUpButtonUI>();

                    // CursorHoverHint es nuevo — se suma también a un panel armado en una
                    // corrida anterior, no solo a uno recién creado.
                    if (buttonTransform.GetComponent<CursorHoverHint>() == null)
                    {
                        buttonTransform.gameObject.AddComponent<CursorHoverHint>();
                    }
                }
            }

            var (installOverlay, installTitleText, installBarFill) = EnsureInstallOverlay(panelGO.transform, uiSprite);

            // El controller vive en el Canvas (siempre activo), NO en el panel: si viviera en
            // panelGO, al arrancar con el panel oculto su propio OnEnable nunca correría y
            // jamás se suscribiría a PlayerLeveledUpEvent.
            var manager = canvas.GetComponent<LevelUpManager>();
            if (manager == null)
            {
                manager = canvas.gameObject.AddComponent<LevelUpManager>();
            }

            SetField(manager, "panelRoot", panelGO);
            SetObjectArray(manager, "buttons", buttons);
            SetObjectArray(manager, "weapons", new Object[] { projectileWeapon, orbitalWeapon });
            SetField(manager, "playerStats", playerStats);
            SetField(manager, "playerHealth", playerHealth);
            SetField(manager, "installOverlay", installOverlay);
            SetField(manager, "installTitleText", installTitleText);
            SetField(manager, "installBarFill", installBarFill);

            panelGO.SetActive(true);
        }

        /// <summary>
        /// La ventanita de "instalando/actualizando" que tapa las 3 opciones apenas se elige
        /// una — texto + barra de progreso, ambos recoloreados por LevelUpManager según qué
        /// clase de mejora sea.
        /// </summary>
        private static (GameObject overlay, Text titleText, Image barFill) EnsureInstallOverlay(Transform panelTransform, Sprite uiSprite)
        {
            var existing = panelTransform.Find("InstallOverlay");
            if (existing != null)
            {
                var existingTitle = existing.Find("Title").GetComponent<Text>();
                var existingBarFill = existing.Find("BarBg/BarFill").GetComponent<Image>();
                return (existing.gameObject, existingTitle, existingBarFill);
            }

            var overlayGO = new GameObject("InstallOverlay", typeof(RectTransform));
            overlayGO.transform.SetParent(panelTransform, false);
            var overlayBg = overlayGO.AddComponent<Image>();
            overlayBg.color = new Color(0f, 0f, 0f, 0.9f);
            StretchFill((RectTransform)overlayGO.transform);

            var titleText = CreateText("Title", overlayGO.transform, string.Empty, 28, Color.white, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 40), new Vector2(800, 120));

            var barBg = CreateImage("BarBg", overlayGO.transform, new Color(1f, 1f, 1f, 0.15f), uiSprite);
            SetRect(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -60), new Vector2(500, 24));

            var barFill = CreateImage("BarFill", barBg.transform, Color.white, uiSprite);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 0f;
            StretchFill(barFill.rectTransform);

            overlayGO.SetActive(false);
            return (overlayGO, titleText, barFill);
        }

        private static void BuildGameEndPanel(Transform canvas)
        {
            if (canvas.Find("GameEndPanel") != null)
            {
                return;
            }

            var panelGO = new GameObject("GameEndPanel", typeof(RectTransform));
            panelGO.SetActive(false);
            panelGO.transform.SetParent(canvas, false);

            var dim = panelGO.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.85f);
            StretchFill(dim.rectTransform);

            var message = CreateText("Message", panelGO.transform, "GAME OVER", 52, Color.white, TextAnchor.MiddleCenter);
            StretchFill(message.rectTransform);

            // Mismo motivo que en LevelUpPanel: el controller va en el Canvas, no en el panel.
            var endUI = canvas.gameObject.AddComponent<GameEndUI>();
            SetField(endUI, "panelRoot", panelGO);
            SetField(endUI, "messageText", message);

            panelGO.SetActive(true);
        }

        private static void EnsureHealthBarFillConfigured(Transform canvas, Sprite flatSprite)
        {
            var fillTransform = canvas.Find("HealthBar_Background/HealthBar_Fill");
            if (fillTransform == null)
            {
                Debug.LogWarning("[GameBootstrap] No se encontró HealthBar_Fill para reconfigurar.");
                return;
            }

            // Se reaplica siempre (no solo si falta): en algún momento este Image quedó con
            // Source Image = None y Type = Simple, lo que hace que fillAmount no tenga ningún
            // efecto visual — la barra se ve siempre llena sin importar la vida real. Además
            // usamos un sprite plano (no UISprite) porque un sprite con bordes/esquinas
            // redondeadas se deforma al estirarlo en una barra angosta.
            var fillImage = fillTransform.GetComponent<Image>();
            fillImage.sprite = flatSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            var xpFillTransform = canvas.Find("XpBar_Background/XpBar_Fill");
            if (xpFillTransform != null)
            {
                xpFillTransform.GetComponent<Image>().sprite = flatSprite;
            }
        }

        private static void EnsureLevelUpButtonSprites(Transform canvas, Sprite flatSprite)
        {
            var panel = canvas.Find("LevelUpPanel");
            if (panel == null)
            {
                return;
            }

            for (var i = 0; i < 3; i++)
            {
                var option = panel.Find($"Option_{i}");
                if (option != null)
                {
                    option.GetComponent<Image>().sprite = flatSprite;
                }
            }
        }

        /// <summary>
        /// Un cuadrado blanco liso de 4x4, guardado como asset real (no un Sprite temporal en
        /// memoria, que se perdería al recargar el dominio). Lo usamos en vez de los sprites
        /// de UI de Unity porque esos tienen bordes/esquinas pensados para botones cuadrados:
        /// al estirarlos en una barra angosta (500x26) se ven deformados.
        /// </summary>
        private static Sprite CreateFlatSpriteAsset()
        {
            const string path = "Assets/Data/UI/FlatWhite.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Data/UI");

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ApplyPhysicsSmoothing(GameObject player, GameObject enemyPrefabAsset, Projectile projectilePrefab, XpGem xpGemPrefab)
        {
            // El titileo al moverse es el clásico problema de mover un Rigidbody2D por física
            // (FixedUpdate, a tasa fija) mientras la cámara y el render van a la tasa de
            // pantalla (variable) — Interpolate le pide a Unity que suavice la posición
            // RENDERIZADA entre pasos de física, sin tocar la simulación en sí.
            SetInterpolation(player.GetComponent<Rigidbody2D>());
            SetInterpolation(enemyPrefabAsset.GetComponent<Rigidbody2D>());
            SetInterpolation(projectilePrefab.GetComponent<Rigidbody2D>());
            SetInterpolation(xpGemPrefab.GetComponent<Rigidbody2D>());
        }

        private static void SetInterpolation(Rigidbody2D rb)
        {
            if (rb == null)
            {
                return;
            }

            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Los prefabs (enemigo, proyectil, gema) no son objetos de escena: sin esto, el
            // cambio puede no quedar guardado en el asset al hacer AssetDatabase.SaveAssets().
            EditorUtility.SetDirty(rb);
            EditorUtility.SetDirty(rb.gameObject);
        }

        private static void EnsureGroundPattern(Sprite flatSprite)
        {
            // El Ground es la "hoja" de la ventana: antes tenía un cuadriculado placeholder
            // (Assets/Sprites/PatronGround.jpeg) sin nada que ver con el tema, después un azul
            // sólido — pero un color plano no da ninguna pista de que te estás moviendo. Un
            // wallpaper fotográfico tampoco sirve (con 40+ enemigos en pantalla el fondo tiene
            // que perder contra el gameplay), así que la solución es un patrón sutil: rayitas
            // diagonales bien tenues, el mismo lenguaje visual que usa Windows para superficies
            // "deshabilitadas" — da referencia de movimiento sin competir con nada.
            var groundGO = GameObject.Find("Ground");
            if (groundGO == null)
            {
                return;
            }

            var renderer = groundGO.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = CreateGroundHatchSpriteAsset();
            renderer.color = Color.white; // el color ya está horneado en la textura

            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = Vector2.one;

            EditorUtility.SetDirty(renderer);

            RetintGroundBorders(flatSprite);
            SetCameraVoidBlack();
            RemoveCameraConfiner();
        }

        /// <summary>
        /// La cámara NO se toca/restringe (sigue libre, igual que siempre) — pero si en
        /// algún momento su encuadre llega a mostrar algo más allá del muro, que sea negro
        /// liso en vez de repetir el azul infinito. El muro (con su bisel) es el único que
        /// tiene que leerse como "el borde de la ventana"; más allá de eso no hay nada.
        /// </summary>
        private static void SetCameraVoidBlack()
        {
            var cameraGO = GameObject.Find("Main Camera");
            if (cameraGO == null)
            {
                return;
            }

            var camera = cameraGO.GetComponent<Camera>();
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            EditorUtility.SetDirty(camera);
        }

        /// <summary>
        /// Vuelve la cámara a como estaba antes de probar con un Confiner2D — el jugador
        /// prefirió que la cámara quedara sin restricciones y que el borde se resuelva
        /// puramente con arte (muro + bisel + fondo negro), no con la cámara.
        /// </summary>
        private static void RemoveCameraConfiner()
        {
            var cameraGO = GameObject.Find("CinemachineCamera");
            if (cameraGO != null)
            {
                var confiner = cameraGO.GetComponent<CinemachineConfiner2D>();
                if (confiner != null)
                {
                    Object.DestroyImmediate(confiner);
                }
            }

            var boundsGO = GameObject.Find("ConfinerBounds");
            if (boundsGO != null)
            {
                Object.DestroyImmediate(boundsGO);
            }
        }

        /// <summary>
        /// Genera (una sola vez) una textura tileable de rayitas diagonales sutiles sobre
        /// azul Luna — mismo espíritu que CreateFlatSpriteAsset, pero con un patrón en vez
        /// de un color plano.
        /// </summary>
        private static Sprite CreateGroundHatchSpriteAsset()
        {
            const string path = "Assets/Data/UI/GroundHatch.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Data/UI");

            const int size = 64;
            const int stripeWidth = 3;
            const int stripeSpacing = 16;
            var baseColor = new Color32(0x3A, 0x6E, 0xA5, 0xFF);
            var stripeColor = new Color32(0x47, 0x82, 0xBE, 0xFF); // apenas más claro, nada de ruido

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var diagonal = (x + y) % stripeSpacing;
                    pixels[y * size + x] = diagonal < stripeWidth ? stripeColor : baseColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Los 4 "Borde" que ya estaban puestos a mano en la escena (los muros que frenan a
        /// jugador y enemigos) — pasan del marrón placeholder al azul de la ventana, y suman
        /// una sombra apenas más grande detrás (mismo objeto, un hijo) para que se lean como
        /// el borde bien delimitado de una ventana real, no como un rectángulo liso.
        /// </summary>
        private static void RetintGroundBorders(Sprite flatSprite)
        {
            var groundGO = GameObject.Find("Ground");
            if (groundGO == null)
            {
                return;
            }

            foreach (Transform child in groundGO.transform)
            {
                if (!child.name.StartsWith("Borde"))
                {
                    continue;
                }

                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = WindowFrameBlue;
                EditorUtility.SetDirty(renderer);

                AddWallShadow(child, renderer, flatSprite);
            }
        }

        /// <summary>
        /// Una silueta más oscura y un poco más grande detrás del muro — le da un borde
        /// visible propio (como el bisel de una ventana real) en vez de ser un bloque de
        /// color liso indistinguible del resto.
        /// </summary>
        private static void AddWallShadow(Transform wallTransform, SpriteRenderer wallRenderer, Sprite flatSprite)
        {
            var shadowTransform = wallTransform.Find("Shadow");
            GameObject shadowGO;

            if (shadowTransform == null)
            {
                shadowGO = new GameObject("Shadow");
                shadowGO.transform.SetParent(wallTransform, false);
                shadowGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            }
            else
            {
                shadowGO = shadowTransform.gameObject;
            }

            var shadowRenderer = shadowGO.GetComponent<SpriteRenderer>();
            if (shadowRenderer == null)
            {
                shadowRenderer = shadowGO.AddComponent<SpriteRenderer>();
            }

            shadowRenderer.sprite = flatSprite;
            shadowRenderer.color = new Color32(0x03, 0x1E, 0x52, 0xFF);
            shadowRenderer.sortingOrder = wallRenderer.sortingOrder - 1;
            EditorUtility.SetDirty(shadowRenderer);
        }

        private static void EnsureHealthBarHpText(Transform canvas)
        {
            var background = canvas.Find("HealthBar_Background");
            var hudGO = GameObject.Find("HUD");
            if (background == null || hudGO == null || background.Find("HealthBar_HpText") != null)
            {
                return;
            }

            var hpText = CreateText("HealthBar_HpText", background, "100/100", 20, Color.white, TextAnchor.MiddleCenter);
            StretchFill(hpText.rectTransform);

            var healthBarUI = hudGO.GetComponent<HealthBarUI>();
            SetField(healthBarUI, "hpText", hpText);
        }

        private static void BuildPlayerStatsText(Transform canvas)
        {
            if (canvas.Find("PlayerStats_Text") != null)
            {
                return;
            }

            var statsGO = new GameObject("PlayerStats_Text", typeof(RectTransform));
            statsGO.SetActive(false);
            statsGO.transform.SetParent(canvas, false);

            var statsText = statsGO.AddComponent<Text>();
            ConfigureText(statsText, "Velocidad: 100%   Daño: 100%", 22, Color.white, TextAnchor.MiddleLeft);
            SetRect(statsText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -150), new Vector2(500, 30));

            var statsUI = statsGO.AddComponent<PlayerStatsUI>();
            SetField(statsUI, "statsText", statsText);

            statsGO.SetActive(true);
        }

        private static void BuildWeaponsHudText(Transform canvas)
        {
            if (canvas.Find("Weapons_Text") != null)
            {
                return;
            }

            var weaponsGO = new GameObject("Weapons_Text", typeof(RectTransform));
            weaponsGO.SetActive(false);
            weaponsGO.transform.SetParent(canvas, false);

            var weaponsText = weaponsGO.AddComponent<Text>();
            ConfigureText(weaponsText, string.Empty, 22, Color.white, TextAnchor.UpperLeft);
            SetRect(weaponsText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -190), new Vector2(500, 130));

            var weaponsUI = weaponsGO.AddComponent<WeaponsHudUI>();
            SetField(weaponsUI, "weaponsText", weaponsText);

            weaponsGO.SetActive(true);
        }

        private static void BuildKillStreakText(Transform canvas, Sprite uiSprite)
        {
            var textTransform = canvas.Find("KillStreak_Text");
            Text text;

            if (textTransform != null)
            {
                text = textTransform.GetComponent<Text>();
            }
            else
            {
                var textGO = new GameObject("KillStreak_Text", typeof(RectTransform));
                textGO.transform.SetParent(canvas, false);

                text = textGO.AddComponent<Text>();
                ConfigureText(text, string.Empty, 34, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter);
                SetRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -110), new Vector2(400, 50));

                textGO.AddComponent<SquashStretch>();
                // Empieza oculto: KillStreakUI (que vive en el Canvas, siempre activo, no acá)
                // lo prende/apaga solo según la racha.
                textGO.SetActive(false);

                textTransform = textGO.transform;
            }

            var barFill = EnsureKillStreakBar(textTransform, uiSprite);

            // El controller va en el Canvas, no en el texto: si viviera en el mismo objeto
            // que se oculta, su propio OnEnable nunca correría y jamás se suscribiría al
            // EventManager (mismo motivo que LevelUpManager/GameEndUI).
            var killStreakUI = canvas.GetComponent<KillStreakUI>();
            if (killStreakUI == null)
            {
                killStreakUI = canvas.gameObject.AddComponent<KillStreakUI>();
            }

            SetField(killStreakUI, "streakText", text);
            SetField(killStreakUI, "streakBarFill", barFill);
        }

        private static Image EnsureKillStreakBar(Transform textTransform, Sprite uiSprite)
        {
            var existingBarBg = textTransform.Find("KillStreak_BarBg");
            if (existingBarBg != null)
            {
                var existingFill = existingBarBg.Find("KillStreak_BarFill");
                return existingFill != null ? existingFill.GetComponent<Image>() : null;
            }

            // Barrita que se vacía debajo del texto de racha, para ver cuánto falta
            // antes de que se corte — vive colgada del texto, así se muestra/oculta junto con él.
            var barBackground = CreateImage("KillStreak_BarBg", textTransform, new Color(0f, 0f, 0f, 0.5f), uiSprite);
            SetRect(barBackground.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, -8), new Vector2(220, 10));

            var barFill = CreateImage("KillStreak_BarFill", barBackground.transform, new Color(1f, 0.85f, 0.2f), uiSprite);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 1f;
            StretchFill(barFill.rectTransform);

            return barFill;
        }

        private static void BuildWaveWarningText(Transform canvas)
        {
            if (canvas.Find("WaveWarning_Text") != null)
            {
                return;
            }

            var textGO = new GameObject("WaveWarning_Text", typeof(RectTransform));
            textGO.transform.SetParent(canvas, false);

            var text = textGO.AddComponent<Text>();
            ConfigureText(text, "¡OLEADA!", 48, new Color(1f, 0.3f, 0.2f), TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 160), new Vector2(600, 70));

            textGO.AddComponent<SquashStretch>();
            // Empieza oculto: WaveWarningUI (en el Canvas, siempre activo) lo prende/apaga
            // solo cuando EnemySpawner publica WaveIncomingEvent.
            textGO.SetActive(false);

            var waveWarningUI = canvas.gameObject.AddComponent<WaveWarningUI>();
            SetField(waveWarningUI, "warningText", text);
        }

        private static void BuildBossHealthBar(Transform canvas, Sprite uiSprite)
        {
            var panelTransform = canvas.Find("BossHealthBar");
            GameObject panelGO;
            Image fill;
            Text nameText;

            if (panelTransform == null)
            {
                panelGO = new GameObject("BossHealthBar", typeof(RectTransform));
                panelGO.transform.SetParent(canvas, false);
                SetRect(panelGO.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -170), new Vector2(700, 50));

                var background = panelGO.AddComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.6f);

                fill = CreateImage("Fill", panelGO.transform, new Color(0.6f, 0.1f, 0.7f), uiSprite);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 1f;
                StretchFill(fill.rectTransform);

                nameText = CreateText("Name", panelGO.transform, "Jefe", 22, Color.white, TextAnchor.MiddleCenter);
                StretchFill(nameText.rectTransform);

                panelGO.SetActive(false);
            }
            else
            {
                panelGO = panelTransform.gameObject;
                fill = panelTransform.Find("Fill").GetComponent<Image>();
                nameText = panelTransform.Find("Name").GetComponent<Text>();
            }

            var loadingOverlay = EnsureBossLoadingOverlay(panelGO.transform, uiSprite);

            var bossBarUI = canvas.GetComponent<BossHealthBarUI>();
            if (bossBarUI == null)
            {
                bossBarUI = canvas.gameObject.AddComponent<BossHealthBarUI>();
            }

            SetField(bossBarUI, "panelRoot", panelGO);
            SetField(bossBarUI, "fillImage", fill);
            SetField(bossBarUI, "nameText", nameText);
            SetField(bossBarUI, "loadingOverlay", loadingOverlay);
        }

        /// <summary>
        /// El "Copiando salud del jefe..." que tapa la barra un instante al aparecer — reusa
        /// los 2 frames que separó Unity de GIF ARCHIVOS.gif (el ícono animado real de
        /// "copiando archivos" de Windows) como un flipbook manual.
        /// </summary>
        private static GameObject EnsureBossLoadingOverlay(Transform panelTransform, Sprite uiSprite)
        {
            var existing = panelTransform.Find("LoadingOverlay");
            if (existing != null)
            {
                return existing.gameObject;
            }

            var overlayGO = new GameObject("LoadingOverlay", typeof(RectTransform));
            overlayGO.transform.SetParent(panelTransform, false);
            var overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.85f);
            StretchFill((RectTransform)overlayGO.transform);

            var frames = LoadAllSpritesSortedByName("Assets/Sprites/GIF ARCHIVOS.gif");

            var iconImage = CreateImage("Icon", overlayGO.transform, Color.white, frames.Length > 0 ? frames[0] : null);
            SetRect(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24, 0), new Vector2(40, 32));
            var flipbook = iconImage.gameObject.AddComponent<SpriteFlipbook>();
            SetField(flipbook, "targetImage", iconImage);
            SetObjectArray(flipbook, "frames", frames);

            var loadingText = CreateText("Text", overlayGO.transform, "Copiando salud del jefe...", 18, Color.white, TextAnchor.MiddleLeft);
            SetRect(loadingText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(72, 0), new Vector2(600, 32));

            overlayGO.SetActive(false);
            return overlayGO;
        }

        private static Sprite[] LoadAllSpritesSortedByName(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
        }

        /// <summary>El chiste de "el jefe termina en la Papelera de reciclaje" — un ícono grande y quieto en el centro, con su propio pop de entrada.</summary>
        private static void BuildBossRecycleBin(Transform canvas)
        {
            if (canvas.Find("BossRecycleBin") != null)
            {
                return;
            }

            var binSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GIF PAPELERA.gif");

            var binGO = new GameObject("BossRecycleBin", typeof(RectTransform));
            binGO.transform.SetParent(canvas, false);

            var binImage = binGO.AddComponent<Image>();
            binImage.sprite = binSprite;
            binImage.raycastTarget = false;
            SetRect(binImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(160, 160));

            binGO.AddComponent<SquashStretch>();
            binGO.SetActive(false);

            var recycleBinUI = canvas.gameObject.AddComponent<BossRecycleBinUI>();
            SetField(recycleBinUI, "binImage", binImage);
        }

        /// <summary>
        /// El pantallazo azul de la derrota — reemplaza al panel de game over genérico solo
        /// para cuando el jugador muere (ganar sigue mostrando el panel normal).
        /// </summary>
        private static void BuildBsod(Transform canvas)
        {
            if (canvas.Find("BsodPanel") != null)
            {
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Perfect DOS VGA 437 SDF.asset");

            var panelGO = new GameObject("BsodPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvas, false);
            StretchFill((RectTransform)panelGO.transform);

            var background = panelGO.AddComponent<Image>();
            background.color = new Color32(0x00, 0x00, 0xAA, 0xFF); // el azul clásico del pantallazo

            var messageGO = new GameObject("Message", typeof(RectTransform));
            messageGO.transform.SetParent(panelGO.transform, false);
            SetRect((RectTransform)messageGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000, 500));

            var messageText = messageGO.AddComponent<TextMeshProUGUI>();
            messageText.font = font;
            messageText.fontSize = 32;
            messageText.color = Color.white;
            messageText.alignment = TextAlignmentOptions.TopLeft;
            messageText.text = string.Empty;

            panelGO.SetActive(false);

            var bsodUI = canvas.gameObject.AddComponent<BsodUI>();
            SetField(bsodUI, "panelRoot", panelGO);
            SetField(bsodUI, "messageText", messageText);
        }

        private static void BuildThreatIndicator(Transform canvas, GameObject enemyPrefabAsset)
        {
            if (canvas.Find("ThreatIndicators") != null)
            {
                return;
            }

            var enemySprite = enemyPrefabAsset.GetComponent<SpriteRenderer>().sprite;

            var rootGO = new GameObject("ThreatIndicators", typeof(RectTransform));
            rootGO.transform.SetParent(canvas, false);

            const int arrowCount = 4;
            var arrows = new RectTransform[arrowCount];
            for (var i = 0; i < arrowCount; i++)
            {
                var arrowImage = CreateImage($"Arrow_{i}", rootGO.transform, new Color(1f, 0.2f, 0.2f), enemySprite);
                arrowImage.rectTransform.sizeDelta = new Vector2(40, 40);
                arrowImage.raycastTarget = false;
                arrowImage.gameObject.SetActive(false);
                arrows[i] = arrowImage.rectTransform;
            }

            var indicatorUI = rootGO.AddComponent<ThreatIndicatorUI>();
            SetObjectArray(indicatorUI, "arrowPool", arrows);
        }

        /// <summary>
        /// La barra de título estilo Windows arriba de toda la pantalla, con los 3 botoncitos
        /// (_, □, X) — puramente decorativa por ahora, pero se pone "inactiva" (gris) cuando
        /// el juego está en pausa (Time.timeScale == 0: panel de nivel, cofre o game over),
        /// el mismo gesto que hace una ventana real de Windows al perder el foco.
        /// </summary>
        /// <summary>
        /// La barra entera (fondo, texto "Sistema.exe" y los 3 botones) cuelga como hijo
        /// DENTRO de WindowFrameSides (el windowContainer) — así, cuando DynamicWindowFrame
        /// achica el contenedor desde arriba, toda la barra baja junto con él sin que ningún
        /// script la toque: son los Anchors nativos de Unity los que la arrastran entera,
        /// intacta, sin deformar ni el texto ni los botones.
        /// </summary>
        private static void BuildWindowTitleBar(Transform canvas)
        {
            var container = canvas.Find("WindowFrameSides");
            if (container == null)
            {
                return;
            }

            // Versiones viejas de este método colgaban la barra directa del Canvas (hermana
            // del contenedor, no hija) — si queda una huérfana de esas, se destruye para no
            // terminar con dos barras superpuestas (la vieja, fija, más la nueva que sí sigue
            // al contenedor).
            var strayBar = canvas.Find("WindowTitleBar");
            if (strayBar != null)
            {
                Object.DestroyImmediate(strayBar.gameObject);
            }

            if (container.Find("WindowTitleBar") != null)
            {
                return;
            }

            var barActive = ExtractCroppedSprite(
                "Assets/Sprites/WinXp/Frame/UI Theme.png", "Assets/Data/UI/WindowChrome/TitleBar_Active.png",
                new RectInt(14, 393, 66, 29));
            var barInactive = ExtractCroppedSprite(
                "Assets/Sprites/WinXp/Frame/UI Theme.png", "Assets/Data/UI/WindowChrome/TitleBar_Inactive.png",
                new RectInt(14, 422, 66, 29));
            var btnMinimize = ExtractCroppedSprite(
                "Assets/Sprites/WinXp/Frame/UI Theme.png", "Assets/Data/UI/WindowChrome/TitleBarBtn_Minimize.png",
                new RectInt(375, 236, 21, 28));
            var btnMaximize = ExtractCroppedSprite(
                "Assets/Sprites/WinXp/Frame/UI Theme.png", "Assets/Data/UI/WindowChrome/TitleBarBtn_Maximize.png",
                new RectInt(399, 236, 21, 28));
            var btnClose = ExtractCroppedSprite(
                "Assets/Sprites/WinXp/Frame/UI Theme.png", "Assets/Data/UI/WindowChrome/TitleBarBtn_Close.png",
                new RectInt(447, 236, 21, 28));

            SetSpriteBorder("Assets/Data/UI/WindowChrome/TitleBar_Active.png", new Vector4(16, 0, 16, 0));
            SetSpriteBorder("Assets/Data/UI/WindowChrome/TitleBar_Inactive.png", new Vector4(16, 0, 16, 0));

            // SetSpriteBorder puede forzar un reimport, que en Unity invalida las referencias
            // Sprite ya obtenidas antes — se vuelven a pedir para asegurarse de usar las vivas.
            barActive = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Data/UI/WindowChrome/TitleBar_Active.png");
            barInactive = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Data/UI/WindowChrome/TitleBar_Inactive.png");

            var barGO = new GameObject("WindowTitleBar", typeof(RectTransform));
            barGO.transform.SetParent(container, false);
            var barRect = (RectTransform)barGO.transform;
            SetRect(barRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, 0), new Vector2(0, TitleBarHeight));

            var barImage = barGO.AddComponent<Image>();
            barImage.sprite = barActive;
            barImage.type = Image.Type.Sliced;
            barImage.raycastTarget = false;

            var titleText = CreateText("Title", barGO.transform, "Sistema.exe", 18, Color.white, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14, 0), new Vector2(300, 24));
            titleText.raycastTarget = false;

            // Los 3 botones cuelgan directo de la barra, anclados a su borde derecho — evita
            // depender de un contenedor intermedio con su propio ancho a calcular a mano.
            CreateTitleBarButton(barGO.transform, "Close", btnClose, new Vector2(-14, 0));
            CreateTitleBarButton(barGO.transform, "Maximize", btnMaximize, new Vector2(-38, 0));
            CreateTitleBarButton(barGO.transform, "Minimize", btnMinimize, new Vector2(-62, 0));

            var titleBarUI = canvas.gameObject.GetComponent<WindowTitleBarUI>();
            if (titleBarUI == null)
            {
                titleBarUI = canvas.gameObject.AddComponent<WindowTitleBarUI>();
            }

            SetField(titleBarUI, "barImage", barImage);
            SetField(titleBarUI, "activeSprite", barActive);
            SetField(titleBarUI, "inactiveSprite", barInactive);
        }

        private static void CreateTitleBarButton(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition)
        {
            var image = CreateImage(name, parent, Color.white, sprite);
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                anchoredPosition, new Vector2(21, 24));
        }

        /// <summary>
        /// Franjas azules en los otros 3 bordes de la pantalla (la de arriba ya la pone
        /// WindowTitleBar) — quedan SIEMPRE visibles, sin importar dónde esté parado el
        /// jugador, para reforzar todo el tiempo "estás adentro de una ventana".
        /// </summary>
        private static void BuildWindowFrameSides(Transform canvas, Sprite flatSprite)
        {
            // Sin early-return: el grosor/color se retocó después de verlo en juego (10px
            // quedaba invisible), así que esto se reaplica siempre, exista ya o no el objeto.
            var color = WindowFrameBlue;
            const float thickness = 18f;

            var rootTransform = canvas.Find("WindowFrameSides");
            GameObject rootGO;

            if (rootTransform == null)
            {
                rootGO = new GameObject("WindowFrameSides", typeof(RectTransform));
                rootGO.transform.SetParent(canvas, false);
                rootGO.transform.SetAsFirstSibling(); // detrás de HUD/paneles, nunca tapando nada

                CreateFrameEdge(rootGO.transform, "Left", flatSprite, color,
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0));
                CreateFrameEdge(rootGO.transform, "Right", flatSprite, color,
                    new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0));
                CreateFrameEdge(rootGO.transform, "Bottom", flatSprite, color,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, thickness));
            }
            else
            {
                rootGO = rootTransform.gameObject;
            }

            // El contenedor ocupa toda la pantalla. "Sistema.exe" NO es un borde más de este
            // loop: BuildWindowTitleBar la cuelga como hijo DENTRO de este mismo contenedor,
            // anclada a su borde superior con su propio alto fijo — cuando el contenedor se
            // achica desde arriba, la barra entera baja con él sola, por Anchors nativos de
            // Unity, sin que este script tenga que tocarla ni reservarle espacio. Left/Right
            // quedan detrás de ella en la franja de arriba (misma anclaje al tope del
            // contenedor), tapados por su sprite opaco — nunca se ve una costura. Sin
            // early-return: se reaplica siempre, exista ya el objeto o no.
            var containerRect = (RectTransform)rootGO.transform;
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            foreach (var edgeName in new[] { "Left", "Right", "Bottom" })
            {
                var edgeTransform = rootGO.transform.Find(edgeName);
                if (edgeTransform == null)
                {
                    continue;
                }

                var image = edgeTransform.GetComponent<Image>();
                image.color = color;

                var sizeDelta = image.rectTransform.sizeDelta;
                image.rectTransform.sizeDelta = edgeName == "Bottom"
                    ? new Vector2(sizeDelta.x, thickness)
                    : new Vector2(thickness, sizeDelta.y);
            }
        }

        private static void CreateFrameEdge(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            var image = CreateImage(name, parent, color, sprite);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.pivot = pivot;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = sizeDelta;
        }

        /// <summary>
        /// Engancha el DynamicWindowFrame: WindowFrameSides ES el "windowContainer" — ya
        /// ocupa toda la pantalla y ya tiene a Left/Right/Bottom anclados a sus bordes con
        /// grosor fijo (ver BuildWindowFrameSides), que es exactamente lo que este script
        /// necesita para achicarlo desde offsetMin/offsetMax sin romper las esquinas.
        /// </summary>
        private static void BuildDynamicWindowFrame(Transform canvas)
        {
            var groundGO = GameObject.Find("Ground");
            var frameSidesTransform = canvas.Find("WindowFrameSides");
            if (groundGO == null || frameSidesTransform == null)
            {
                return;
            }

            var groundRenderer = groundGO.GetComponent<SpriteRenderer>();
            var cameraGO = GameObject.Find("Main Camera");
            if (groundRenderer == null || cameraGO == null)
            {
                return;
            }

            var bounds = groundRenderer.bounds;

            // Versiones viejas de este script tenían otros campos (leftEdge/rightEdge/etc.) —
            // limpia cualquier componente huérfano que haya quedado de una clase renombrada.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);

            var frame = canvas.GetComponent<DynamicWindowFrame>();
            if (frame == null)
            {
                frame = canvas.gameObject.AddComponent<DynamicWindowFrame>();
            }

            SetField(frame, "targetCamera", cameraGO.GetComponent<Camera>());
            SetField(frame, "windowContainer", (RectTransform)frameSidesTransform);

            frame.ConfigureBounds(bounds.min.x, bounds.max.x, bounds.min.y, bounds.max.y);
            EditorUtility.SetDirty(frame);
        }

        /// <summary>
        /// Filtro de "monitor CRT viejo" (scanlines, curvatura, aberración cromática,
        /// vineteado, barrido, parpadeo) sobre TODA la pantalla, UI incluida — vía
        /// CrtPostProcess.cs (Graphics.Blit clásico), NO un Renderer Feature.
        ///
        /// La clave para que la UI quede adentro: el Canvas del juego usa Screen Space -
        /// Camera apuntando a la MISMA Main Camera (nunca Screen Space - Overlay), y Main
        /// Camera renderiza a su propio RenderTexture (targetTexture) en vez de a la
        /// pantalla — CrtPostProcess.Update() corre Graphics.Blit sobre esa textura (mundo +
        /// UI ya mezclados) y muestra el resultado en un RawImage de pantalla completa, el
        /// único paso que toca la pantalla real.
        ///
        /// Por qué NO un Full Screen Pass Renderer Feature (probado a mano, los tres
        /// injection points posibles, en este Renderer2D de Unity 6 / URP 17.0.4):
        /// - "Before/After Rendering Post Processing": fetchColorBuffer copia un buffer
        ///   vacío (gris plano) — confirmado con un shader de passthrough puro sin ninguna
        ///   matemática propia, que daba el mismo gris. Un límite real de cómo Renderer2D
        ///   arma sus recursos de RenderGraph ahí, no un bug de este shader.
        /// - "Before Rendering Transparents": sí lee el color real, pero lo que ese pase
        ///   dibuja se termina tapando con el redibujado normal de sprites/UI que viene
        ///   después — confirmado con un shader que devuelve rojo sólido: nunca se vio rojo
        ///   en pantalla, siempre la imagen normal sin procesar.
        /// Ningún injection point da lectura Y escritura correctas del color a la vez.
        /// Graphics.Blit corre afuera de todo RenderGraph, así que ninguno de estos límites
        /// aplica — ver CrtPostProcess.cs para el detalle completo (incluido el fix de
        /// _BlitScaleBias que Graphics.Blit clásico no setea solo).
        ///
        /// Caminos más viejos, todos abandonados por problemas reales — ver historial
        /// completo en CrtPostProcess.cs y en el propio shader:
        /// 1) Full Screen Pass Renderer Feature con el Canvas en Screen Space - Overlay:
        ///    ningún injection point ve la UI en absoluto (aparte de lo de arriba).
        /// 2) RenderPipelineManager.endContextRendering + context.Submit(): llegó a andar en
        ///    una sesión, pero en la sesión siguiente no mostró nada real — depende de
        ///    estado interno de RenderGraph no garantizado frame a frame.
        /// 3) Dos cámaras separadas (mundo + UI) compuestas a mano con
        ///    Blitter.BlitCameraTexture: mismo síntoma de pantalla plana que los injection
        ///    points rotos de arriba (probablemente la MISMA causa de fondo).
        /// 4) ScreenCapture tras WaitForEndOfFrame mostrado en un RawImage de pantalla
        ///    completa: LOOP DE RETROALIMENTACIÓN (el RawImage opaco queda dentro de lo que
        ///    se captura al frame siguiente) — converge rápido a un color plano.
        ///
        /// Idempotente: si el Material ya existe lo reusa tal cual (nunca pisa valores que el
        /// pase de arte ya haya tuneado a mano); limpia cualquier resto de los intentos
        /// anteriores (UI Camera dedicada, Renderer Feature, CrtCompositor).
        /// </summary>
        private static void BuildCrtScanlineEffect()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CRTScanlines.shader");
            if (shader == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró Assets/Shaders/CRTScanlines.shader.");
                return;
            }

            EnsureFolder("Assets/Data/Fx");

            const string materialPath = "Assets/Data/Fx/CRT_Scanlines.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CRT_Scanlines" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            RemoveObsoleteCrtLeftovers();

            var cameraGO = GameObject.Find("Main Camera");
            var canvasGO = GameObject.Find("Canvas");
            if (cameraGO == null || canvasGO == null)
            {
                Debug.LogError("[GameBootstrap] No se encontró Main Camera o Canvas.");
                return;
            }

            var mainCamera = cameraGO.GetComponent<Camera>();

            // CrtPostProcess se lo asigna solo en tiempo de juego (OnEnable/Update no corren
            // en Editor sin [ExecuteAlways]) — acá se deja en null a propósito, así la escena
            // guardada siempre arranca con Main Camera dibujando derecho a pantalla.
            mainCamera.targetTexture = null;

            // Restos de un intento anterior podían haber sacado la layer UI del culling mask
            // de Main Camera (para que la dibujara una cámara aparte) — ahora que la misma
            // Main Camera dibuja el Canvas, tiene que volver a incluirla.
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                mainCamera.cullingMask |= 1 << uiLayer;
            }

            // ---- Canvas del juego: Screen Space - Camera apuntando a la MISMA Main Camera ----
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 1f;

            // ---- Canvas overlay dedicado solo para MOSTRAR el resultado ya procesado ----
            var overlayTransform = GameObject.Find("CrtDisplayCanvas")?.transform;
            GameObject overlayGO;

            if (overlayTransform == null)
            {
                overlayGO = new GameObject("CrtDisplayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

                var overlayCanvas = overlayGO.GetComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 32000; // por encima de cualquier Canvas propio del juego

                var scaler = overlayGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            }
            else
            {
                overlayGO = overlayTransform.gameObject;
            }

            var displayTransform = overlayGO.transform.Find("Display");
            RawImage displayImage;

            if (displayTransform == null)
            {
                var displayGO = new GameObject("Display", typeof(RectTransform), typeof(RawImage));
                displayGO.transform.SetParent(overlayGO.transform, false);
                StretchFill((RectTransform)displayGO.transform);

                displayImage = displayGO.GetComponent<RawImage>();
                displayImage.raycastTarget = false;
            }
            else
            {
                displayImage = displayTransform.GetComponent<RawImage>();
            }

            // Arranca apagado: CrtPostProcess (OnEnable/OnDisable) lo prende solo mientras
            // corre — sin Play no hay nada blitteando, y un RawImage activo con textura null
            // se dibuja blanco sólido, tapando toda la Game view en el Editor.
            displayImage.enabled = false;

            // ---- El componente que blittea + muestra ----
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(overlayGO);

            var postProcess = overlayGO.GetComponent<CrtPostProcess>();
            if (postProcess == null)
            {
                postProcess = overlayGO.AddComponent<CrtPostProcess>();
            }

            SetField(postProcess, "material", material);
            SetField(postProcess, "worldCamera", mainCamera);
            SetField(postProcess, "displayImage", displayImage);

            EditorUtility.SetDirty(overlayGO);
            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Limpia todo rastro de intentos anteriores: la UI Camera dedicada (del intento de
        /// dos cámaras), el FullScreenPassRendererFeature (del intento de Renderer Feature)
        /// en Renderer2D.asset, y cualquier componente que haya quedado "missing script" en
        /// Main Camera / CrtDisplayCanvas por los cambios de clase. Idempotente.
        /// </summary>
        private static void RemoveObsoleteCrtLeftovers()
        {
            RemoveGameObjectIfPresent("UI Camera");

            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/Renderer2D.asset");
            if (rendererData != null)
            {
                var removedAny = false;
                for (var i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
                {
                    var obsoleteFeature = rendererData.rendererFeatures[i] as FullScreenPassRendererFeature;
                    if (obsoleteFeature == null)
                    {
                        continue;
                    }

                    rendererData.rendererFeatures.RemoveAt(i);
                    AssetDatabase.RemoveObjectFromAsset(obsoleteFeature);
                    Object.DestroyImmediate(obsoleteFeature, true);
                    removedAny = true;
                }

                if (removedAny)
                {
                    rendererData.SetDirty();
                    EditorUtility.SetDirty(rendererData);
                }
            }

            var cameraGO = GameObject.Find("Main Camera");
            if (cameraGO != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cameraGO);
            }

            var overlayGO = GameObject.Find("CrtDisplayCanvas");
            if (overlayGO != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(overlayGO);
            }
        }

        /// <summary>
        /// El cursor de mouse que solo se ve mientras hay un menú abierto (panel de nivel/
        /// cofre) — el resto del tiempo el Player mismo hace de cursor en el mundo
        /// (ver BuildPlayerCursorSkin) y este queda oculto.
        /// </summary>
        private static void BuildCursorController(Transform canvas)
        {
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/default_arrow.png");
            var linkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/link_m.png");

            var cursorTransform = canvas.Find("GameCursor");
            Image cursorImage;

            if (cursorTransform == null)
            {
                var cursorGO = new GameObject("GameCursor", typeof(RectTransform));
                cursorGO.transform.SetParent(canvas, false);

                cursorImage = cursorGO.AddComponent<Image>();
                cursorImage.raycastTarget = false;
                cursorImage.rectTransform.sizeDelta = new Vector2(32, 32);
                cursorImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                cursorImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                cursorImage.rectTransform.pivot = new Vector2(0.3f, 0.7f); // punta del cursor, no el centro

                cursorTransform = cursorGO.transform;
            }
            else
            {
                cursorImage = cursorTransform.GetComponent<Image>();
            }

            cursorTransform.SetAsLastSibling(); // siempre encima de todo lo demás, exista o no ya
            cursorImage.sprite = arrowSprite;

            var cursorController = canvas.GetComponent<CursorController>();
            if (cursorController == null)
            {
                cursorController = canvas.gameObject.AddComponent<CursorController>();
            }

            SetField(cursorController, "cursorImage", cursorImage);
            SetField(cursorController, "arrowSprite", arrowSprite);
            SetField(cursorController, "linkSprite", linkSprite);
        }

        /// <summary>
        /// Los cursores de Windows vienen con mucho margen transparente alrededor del dibujo
        /// en sí (a diferencia del círculo viejo, que llenaba todo su sprite) — al mismo
        /// tamaño de antes, el Player se veía diminuto. Bajarles el Pixels Per Unit los hace
        /// renderizar más grandes en el mundo sin tocar ningún Transform (el cursor del menú
        /// no se ve afectado: ese usa un tamaño de píxeles fijo en su RectTransform, no PPU).
        /// </summary>
        private static void EnsureCursorSpritesScale()
        {
            const string folder = "Assets/Sprites/WinXp/Cursor/";
            const float playerCursorPixelsPerUnit = 32f;

            EnsureSpritePixelsPerUnit(folder + "default_arrow.png", playerCursorPixelsPerUnit);
            EnsureSpritePixelsPerUnit(folder + "default_busy.png", playerCursorPixelsPerUnit);
            EnsureSpritePixelsPerUnit(folder + "default_no.png", playerCursorPixelsPerUnit);
            EnsureSpritePixelsPerUnit(folder + "default_move.png", playerCursorPixelsPerUnit);
            EnsureSpritePixelsPerUnit(folder + "link_m.png", playerCursorPixelsPerUnit);
        }

        private static void EnsureSpritePixelsPerUnit(string path, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null || Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
            {
                return;
            }

            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// El Player ES el cursor de Windows: reemplaza su sprite (antes un círculo verde
        /// con ojitos) por la flecha, con los mismos estados de antes pero sin rotación —
        /// WASD lo mueve, nunca lo gira. Los "Ojito" quedan ocultos, no borrados (por si en
        /// algún momento se quiere volver atrás).
        /// </summary>
        private static void BuildPlayerCursorSkin(GameObject player, Health playerHealth, PlayerDash playerDash)
        {
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/default_arrow.png");
            var busySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/default_busy.png");
            var blockedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/default_no.png");
            var moveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/default_move.png");
            var linkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WinXp/Cursor/link_m.png");

            var renderer = player.GetComponent<SpriteRenderer>();
            renderer.sprite = arrowSprite;
            renderer.color = Color.white;

            foreach (Transform child in player.transform)
            {
                if (child.name.StartsWith("Ojito"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            var skin = EnsureComponent<PlayerCursorSkin>(player);
            SetField(skin, "arrowSprite", arrowSprite);
            SetField(skin, "busySprite", busySprite);
            SetField(skin, "blockedSprite", blockedSprite);
            SetField(skin, "moveSprite", moveSprite);
            SetField(skin, "linkSprite", linkSprite);
            SetField(skin, "playerHealth", playerHealth);
            SetField(skin, "playerDash", playerDash);
        }

        /// <summary>
        /// Recorta un rectángulo de una textura ya importada y lo guarda como un Sprite propio
        /// y persistente (mismo espíritu que CreateFlatSpriteAsset). El rect se da en espacio
        /// "de imagen" (0,0 arriba-izquierda, Y creciendo hacia abajo, como cualquier editor de
        /// imágenes) — la conversión al espacio de Unity (0,0 abajo-izquierda) es interna.
        /// </summary>
        private static Sprite ExtractCroppedSprite(string sourceTexturePath, string outputPath, RectInt imageSpaceRect)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (existing != null)
            {
                return existing;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(sourceTexturePath);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceTexturePath);
            var flippedY = sourceTexture.height - imageSpaceRect.y - imageSpaceRect.height;
            var pixels = sourceTexture.GetPixels(imageSpaceRect.x, flippedY, imageSpaceRect.width, imageSpaceRect.height);

            var cropped = new Texture2D(imageSpaceRect.width, imageSpaceRect.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();

            EnsureFolder(Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
            File.WriteAllBytes(outputPath, cropped.EncodeToPNG());
            Object.DestroyImmediate(cropped);

            AssetDatabase.ImportAsset(outputPath);
            var newImporter = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            newImporter.textureType = TextureImporterType.Sprite;
            newImporter.spriteImportMode = SpriteImportMode.Single;
            newImporter.filterMode = FilterMode.Bilinear;
            newImporter.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        }

        private static void SetSpriteBorder(string spritePath, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            if (importer == null || importer.spriteBorder == border)
            {
                return;
            }

            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static void ApplyBalanceTuning(Health playerHealth, PlayerLeveling playerLeveling, XpGem xpGemPrefab, EnemySpawner spawner)
        {
            SetField(playerHealth, "invulnerabilityDuration", 0.5f);

            SetField(playerLeveling, "baseXpToLevel", 25);
            SetField(playerLeveling, "xpCurveMultiplier", 1.35f);

            SetField(xpGemPrefab, "magnetRadius", 5f);
            SetField(xpGemPrefab, "magnetSpeed", 12f);

            SetField(spawner, "spawnRadiusMin", 18f);
            SetField(spawner, "spawnRadiusMax", 24f);
        }

        // ---------------- UI helpers ----------------

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            return image;
        }

        private static void ConfigureText(Text text, string content, int fontSize, Color color, TextAnchor alignment)
        {
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            ConfigureText(text, content, fontSize, color, alignment);
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Sprite uiSprite)
        {
            var image = CreateImage(name, parent, new Color(0.12f, 0.12f, 0.12f, 0.95f), uiSprite);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText("Label", image.transform, label, 26, Color.white, TextAnchor.MiddleCenter);
            StretchFill(text.rectTransform);

            return button;
        }

        // ---------------- Utilidades genéricas ----------------

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void RemoveGameObjectIfPresent(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[GameBootstrap] Campo '{fieldName}' no encontrado en {target.GetType().Name}.");
                return;
            }

            switch (value)
            {
                case null:
                    prop.objectReferenceValue = null;
                    break;
                case Object o:
                    prop.objectReferenceValue = o;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case bool b:
                    prop.boolValue = b;
                    break;
                case string s:
                    prop.stringValue = s;
                    break;
                case Color c:
                    prop.colorValue = c;
                    break;
                default:
                    Debug.LogError($"[GameBootstrap] Tipo no soportado para '{fieldName}': {value.GetType()}");
                    break;
            }

            so.ApplyModifiedProperties();
        }

        private static void SetObjectArray(Object target, string fieldName, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            prop.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedProperties();
        }

        private static void SetWaveEntries(EnemySpawner spawner, string fieldName, params (EnemyData data, float unlockTime)[] entries)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty(fieldName);
            prop.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Data").objectReferenceValue = entries[i].data;
                element.FindPropertyRelative("UnlockTimeSeconds").floatValue = entries[i].unlockTime;
            }

            so.ApplyModifiedProperties();
        }

        private static void SetBossSpawns(EnemySpawner spawner, string fieldName, params (EnemyData data, float spawnTime)[] entries)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty(fieldName);
            prop.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Data").objectReferenceValue = entries[i].data;
                element.FindPropertyRelative("SpawnTimeSeconds").floatValue = entries[i].spawnTime;
            }

            so.ApplyModifiedProperties();
        }

        private static void SetBurstSpawns(EnemySpawner spawner, string fieldName, params (float triggerTime, int count)[] entries)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty(fieldName);
            prop.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("TriggerTimeSeconds").floatValue = entries[i].triggerTime;
                element.FindPropertyRelative("EnemyCount").intValue = entries[i].count;
            }

            so.ApplyModifiedProperties();
        }
    }
}

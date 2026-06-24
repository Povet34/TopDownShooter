using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Player 통합 테스트 (TDS.Game asmdef 참조로 게임 타입 직접 사용 가능).
    /// 스폰 → 서브시스템 배선 확인 → 무기 장착 → 실제 사격(총알 스폰)까지.
    /// </summary>
    public class PlayerCombatTests
    {
        private Player SpawnPlayer()
        {
            GameBootstrap.EnsureSystems(); // ControlsManager 등 보장
            var prefab = Resources.Load<GameObject>("Player");
            Assert.IsNotNull(prefab, "Resources/Player 로드 실패");
            var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            return go.GetComponent<Player>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var p in Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            // 사격 테스트가 남긴 총알(풀에서 꺼내져 parent=null 루트) 정리 — 다음 테스트 오염 방지
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_spawns_with_subsystems_and_controls_wired()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            Assert.IsNotNull(player.movement, "movement 미배선");
            Assert.IsNotNull(player.weapon, "weapon 미배선");
            Assert.IsNotNull(player.aim, "aim 미배선");
            Assert.IsNotNull(player.health, "health 미배선");
            Assert.IsNotNull(player.interaction, "interaction 미배선");
            Assert.IsNotNull(player.controls, "controls 미배선(ControlsManager에서)");
        }

        [UnityTest]
        public IEnumerator Player_controls_have_expected_actions()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var c = player.controls;
            Assert.IsNotNull(c, "controls null");
            Assert.IsNotNull(c.Character.Movement, "Movement 액션 없음");
            Assert.IsNotNull(c.Character.Fire, "Fire 액션 없음");
            Assert.IsNotNull(c.Character.Reload, "Reload 액션 없음");
            Assert.IsNotNull(c.Character.Aim, "Aim 액션 없음");
            Assert.IsNotNull(c.Character.Run, "Run 액션 없음");
            Assert.IsNotNull(c.Character.Interaction, "Interaction 액션 없음");
        }

        [UnityTest]
        public IEnumerator Player_switches_to_second_weapon_slot()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var wc = player.weapon;
            var defaults = typeof(Player_WeaponController)
                .GetField("defaultWeaponData", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(wc) as List<Weapon_Data>;
            Assert.IsTrue(defaults != null && defaults.Count >= 2, "기본 무기 2개 이상 필요");
            wc.SetDefaultWeapon(defaults);
            for (int i = 0; i < 3; i++) yield return null;

            var first = wc.CurrentWeapon().weaponType;
            typeof(Player_WeaponController)
                .GetMethod("EquipWeapon", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(wc, new object[] { 1 });
            yield return null;

            var second = wc.CurrentWeapon().weaponType;
            Assert.AreNotEqual(first, second, "2번 슬롯으로 무기 전환 안됨");
        }

        [UnityTest]
        public IEnumerator Player_takes_damage_reduces_health()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var hp = player.health;
            Assert.Greater(hp.maxHealth, 1, "maxHealth 미설정(프리팹)");
            int before = hp.currentHealth;

            hp.ReduceHealth(1);
            yield return null;

            Assert.AreEqual(before - 1, hp.currentHealth, "체력이 줄지 않음");
            Assert.IsFalse(hp.isDead, "이 피해로 죽으면 안됨");
        }

        [UnityTest]
        public IEnumerator Player_movement_input_moves_player()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            player.SetControlsEnabledTo(true); // 컨트롤 활성화(Player_Movement가 controlsEnabled 가드함)

            var mv = player.movement;
            var moveField = typeof(Player_Movement).GetField("<moveInput>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(moveField, "moveInput 백킹필드 못찾음");

            float startX = player.transform.position.x;
            for (int i = 0; i < 12; i++)
            {
                moveField.SetValue(mv, new Vector2(1f, 0f)); // 입력 홀드 시뮬
                yield return null;
            }

            Assert.Greater(player.transform.position.x, startX + 0.05f, "입력해도 이동하지 않음");
        }

        [UnityTest]
        public IEnumerator Player_equips_default_weapon_and_fires_a_bullet()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            // 프리팹에 직렬화된 기본 무기로 장착
            var wc = player.weapon;
            var dwdField = typeof(Player_WeaponController).GetField("defaultWeaponData", BindingFlags.NonPublic | BindingFlags.Instance);
            var defaults = dwdField.GetValue(wc) as List<Weapon_Data>;
            Assert.IsTrue(defaults != null && defaults.Count > 0, "프리팹 기본 무기 없음");
            wc.SetDefaultWeapon(defaults);

            // 장착/무기 모델 활성화 대기
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsNotNull(wc.CurrentWeapon(), "currentWeapon null");
            wc.SetWeaponReady(true); // 평소엔 장착 애니메이션 이벤트가 설정 — 테스트선 강제

            // 실제 사격: FireSingleBullet 호출 → 총알 스폰 확인
            int before = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var fire = typeof(Player_WeaponController).GetMethod("FireSingleBullet", BindingFlags.NonPublic | BindingFlags.Instance);
            fire.Invoke(wc, null);
            int after = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Assert.Greater(after, before, "사격 시 총알이 스폰되지 않음");
        }

        // 플레이어 CharacterController는 적 레이어와 충돌을 무시한다(적 몸 타고 솟구침 방지).
        [UnityTest]
        public IEnumerator Player_ignores_collision_with_enemy_layer()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy 레이어가 없음");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(player.gameObject.layer, enemyLayer),
                "Player↔Enemy 레이어 충돌이 무시되지 않음(끼임/솟구침 방지 실패)");
        }

        // 적 레이어 몸과 겹쳐 전진해도 위로 솟구치지/올라타지 않는다.
        [UnityTest]
        public IEnumerator Player_does_not_climb_enemy_layer_body()
        {
            GameServices.ResetForTests();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);

            var player = SpawnPlayer(); // (0,0,0)
            yield return null;
            var cc = player.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "CharacterController 없음");

            // 진로(+x)에 적 레이어 캡슐 몸 배치(겹침 유발)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.layer = LayerMask.NameToLayer("Enemy");
            body.transform.position = new Vector3(1f, 0.5f, 0f);

            float startY = player.transform.position.y;
            float maxY = startY;
            for (int i = 0; i < 40; i++)
            {
                cc.Move(new Vector3(0.06f, -0.2f, 0f)); // 전진 + 중력
                maxY = Mathf.Max(maxY, player.transform.position.y);
                yield return null;
            }

            float crossedX = player.transform.position.x;
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(floor);

            Assert.Greater(crossedX, 0.9f, "전진하지 못함(테스트 전제 실패)");
            Assert.Less(maxY, startY + 0.5f, $"적 레이어 몸을 타고 솟구침 (maxY={maxY:0.00}, start={startY:0.00})");
        }

        // 낮은 prop(0.4m, 맵 최저 prop 수준)을 타고 올라가지 않는다 — 실제 이동 경로(Y-lock)로 검증.
        [UnityTest]
        public IEnumerator Player_does_not_climb_low_prop()
        {
            GameServices.ResetForTests();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);

            var player = SpawnPlayer(); // (0,0,0)
            yield return null;
            player.SetControlsEnabledTo(true);

            // 진로(+x)에 0.4m 높이 단(맵 최저 prop ~0.38=concrete_tube/rock 수준).
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.localScale = new Vector3(1f, 0.4f, 6f);
            prop.transform.position = new Vector3(1.5f, 0.2f, 0f);

            var mv = player.movement;
            var moveField = typeof(Player_Movement).GetField("<moveInput>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

            float startY = player.transform.position.y;
            float maxY = startY;
            for (int i = 0; i < 60; i++)
            {
                moveField.SetValue(mv, new Vector2(1f, 0f)); // +x 입력 홀드(ApplyMovement가 Y-lock 적용)
                yield return null;
                maxY = Mathf.Max(maxY, player.transform.position.y);
            }

            float endX = player.transform.position.x;
            Object.DestroyImmediate(prop);
            Object.DestroyImmediate(floor);

            Assert.Less(maxY, startY + 0.15f, $"낮은 prop을 타고 올라감 (maxY={maxY:0.00}, start={startY:0.00})");
            Assert.Less(endX, 1.3f, $"prop에 막히지 않고 통과/등반함 (endX={endX:0.00})");
        }

        [UnityTest]
        public IEnumerator BulletDirection_is_stable_when_aiming_at_own_feet()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;
            player.SetControlsEnabledTo(true);

            var wc = player.weapon;
            var defaults = typeof(Player_WeaponController).GetField("defaultWeaponData", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wc) as List<Weapon_Data>;
            wc.SetDefaultWeapon(defaults);
            for (int i = 0; i < 5; i++) yield return null;
            wc.SetWeaponReady(true);

            var ac = player.aim;
            // 비정밀(수평) 조준 강제
            typeof(Player_AimController).GetField("isAimingPrecisly", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ac, false);

            var gunPoint = wc.GunPoint();
            // 조준점(aim transform)을 총구 바로 아래(XZ 동일, y=0)로 = 발밑. 같은 프레임에 즉시 호출(UpdateAimPosition이 덮기 전).
            ac.Aim().position = new Vector3(gunPoint.position.x, 0f, gunPoint.position.z);

            Vector3 dir = wc.BulletDirection();
            // 가드 전: (0,-Δy,0)→y=0→(0,0,0) zero/랜덤. 가드 후: 안정된 단위 수평 벡터.
            Assert.AreEqual(1f, dir.magnitude, 0.02f, "발밑 조준 시 발사 방향이 0/랜덤(가드 실패)");
            Assert.AreEqual(0f, dir.y, 1e-3f, "비정밀 조준 발사는 수평이어야");
            yield return null;
        }
    }
}

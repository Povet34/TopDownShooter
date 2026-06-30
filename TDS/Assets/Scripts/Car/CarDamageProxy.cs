using UnityEngine;

/// <summary>
/// 탑승 중 차에 붙는 <b>Player 레이어 히트박스</b> — 받은 데미지를 차의 <see cref="Car_HealthController"/>로
/// 전달한다. 몬스터 근접 공격은 <c>OverlapSphere(whatIsPlayer)</c>로 대상을 찾으므로, 운전자(0.01배로
/// 작아져 잘 안 맞음) 대신 차 크기의 이 프록시를 때리게 해 "몬스터가 차를 공격"하게 만든다. 하차 시 제거.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CarDamageProxy : MonoBehaviour, IDamagable
{
    [SerializeField] private Car_HealthController car;

    public void Init(Car_HealthController carHealth) => car = carHealth;

    public void TakeDamage(int damage)
    {
        if (car != null)
            car.TakeDamage(damage);
    }
}

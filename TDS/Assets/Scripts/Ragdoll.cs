using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ragdoll : MonoBehaviour
{
    [SerializeField] private Transform ragdollParent;

    private Collider[] ragdollColliders;
    private Rigidbody[] ragdollRigidbodies;

    private void Awake()
    {
        ragdollColliders = GetComponentsInChildren<Collider>();
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();

        RagdollActive(false);

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public void RagdollActive(bool active)
    {
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = !active;
        }
    }

    /// <summary>래그돌 물리를 멈춰 현재 포즈로 고정(isKinematic=true). 사망 후 일정 시간 뒤 호출해 끝없는 슬라이딩 방지.</summary>
    public void Freeze() => RagdollActive(false);

    /// <summary>모든 래그돌 리지드바디가 고정(kinematic) 상태인지.</summary>
    public bool IsFrozen
    {
        get
        {
            foreach (Rigidbody rb in ragdollRigidbodies)
                if (!rb.isKinematic) return false;
            return ragdollRigidbodies.Length > 0;
        }
    }

    public void CollidersActive(bool active)
    {
        foreach (Collider cd in ragdollColliders)
        {
            cd.enabled = active;
        }
    }
}

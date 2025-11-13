using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FootSteps_Visuals : MonoBehaviour
{
    [SerializeField] LayerMask whatIGround;

    [SerializeField] TrailRenderer leftFoot;
    [SerializeField] TrailRenderer rightFoot;

    [SerializeField, Range(0.001f, 0.3f)] float checkRadius = 0.05f;

    [SerializeField, Range(-0.15f, 0.15f)] float rayDist = -0.05f;

    private void Update()
    {
        CheckFootStep(leftFoot);
        CheckFootStep(rightFoot);
    }
    void CheckFootStep(TrailRenderer foot)
    {
        var checkPos = foot.transform.position + Vector3.down * rayDist;
        bool touchingGround = Physics.CheckSphere(checkPos, checkRadius, whatIGround);

        foot.emitting = touchingGround;
    }

    private void OnDrawGizmos()
    {
        DrawFootGizmozs(leftFoot);
        DrawFootGizmozs(rightFoot);
    }

    void DrawFootGizmozs(TrailRenderer foot)
    {
        if(foot)
        {
            Gizmos.color = Color.blue;
            var checkPos = foot.transform.position + Vector3.down * rayDist;
            Gizmos.DrawWireSphere(checkPos, checkRadius);
        }
    }
}

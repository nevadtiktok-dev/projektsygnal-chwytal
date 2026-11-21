using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ForestMonster : MonoBehaviour
{
    [SerializeField] private AudioSource attackAudioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private Animator animator;
    [SerializeField, Min(1)] private int health = 50;
    [SerializeField, Min(0f)] private float speed = 1.7f;
    [SerializeField, Min(1f)] private float turnSpeed = 240f;
    [SerializeField, Min(0.1f)] private float attackInterval = 2f;
    [SerializeField, Min(1)] private int attackDamage = 10;

    private CharacterController controller;
    private ForestCombat target;
    private float verticalSpeed;
    private float nextAttack;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

    }

    public void BeginChase(ForestCombat player)
    {
        target = player;
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || health <= 0)
            return;

        if (!target.TryGetComponent<CharacterController>(out var playerBody))
            return;

        Vector3 monsterCenter = transform.TransformPoint(controller.center);
        Vector3 playerCenter = playerBody.transform.TransformPoint(playerBody.center);

        Vector3 toPlayer = playerCenter - monsterCenter;
        toPlayer.y = 0f;

        Vector3 movement = Vector3.zero;

        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            Vector3 direction = toPlayer.normalized;
            movement = direction * speed;

            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion correctedFacing = facing * Quaternion.Euler(0f, -90f, 0f);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, correctedFacing, turnSpeed * Time.deltaTime);
        }

        verticalSpeed = controller.isGrounded
            ? -2f
            : verticalSpeed - 20f * Time.deltaTime;

        movement.y = verticalSpeed;
        controller.Move(movement * Time.deltaTime);
        if (animator != null)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            animator.SetBool("IsMoving", velocity.sqrMagnitude > 0.01f);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (target == null || !target.IsAlive || health <= 0 || Time.time < nextAttack)
            return;

        if (!target.TryGetComponent<CharacterController>(out var playerBody) ||
            hit.collider != playerBody)
            return;

        Vector3 separation = controller.bounds.center - playerBody.bounds.center;
        separation.y = 0f;

        if (separation.sqrMagnitude > 1f)
            return;

        nextAttack = Time.time + attackInterval;
        target.TakeDamage(attackDamage);
        if (attackAudioSource != null && attackSound != null)
            attackAudioSource.PlayOneShot(attackSound);
    }

    public void TakeHit(int amount)
    {
        health -= amount;
        if (health <= 0)
            Destroy(gameObject);
    }
}
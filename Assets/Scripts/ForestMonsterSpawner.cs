using System.Collections.Generic;
using UnityEngine;

public class ForestMonsterSpawner : MonoBehaviour
{
    [SerializeField] private ForestCombat player;
    [SerializeField] private ForestMonster monsterPrefab;
    [SerializeField] private Collider ground;
    [SerializeField, Min(1)] private int maxAlive = 6;
    [SerializeField, Min(0.1f)] private float spawnInterval = 7f;
    [SerializeField, Min(1f)] private float minDistance = 24f;
    [SerializeField, Min(1f)] private float maxDistance = 60f;
    [SerializeField, Range(0f, 80f)] private float maxSlope = 30f;

    private readonly List<ForestMonster> alive = new List<ForestMonster>();
    private float nextSpawn;

    private void Start()
    {
        if (player == null || monsterPrefab == null || ground == null)
        {
            Debug.LogError("Forest Monster Spawner: przypisz Player, Monster Prefab i Ground.", this);
            enabled = false;
            return;
        }

        nextSpawn = Time.time + 2f;
    }

    private void Update()
    {
        if (!player.IsAlive || Time.time < nextSpawn)
            return;

        nextSpawn = Time.time + spawnInterval;
        alive.RemoveAll(monster => monster == null);
        if (alive.Count >= maxAlive)
            return;

        Bounds bounds = ground.bounds;
        Vector3 playerPosition = player.transform.position;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minDistance, Mathf.Max(minDistance, maxDistance));
            Vector3 origin = new Vector3(
                playerPosition.x + Mathf.Cos(angle) * radius,
                bounds.max.y + 2f,
                playerPosition.z + Mathf.Sin(angle) * radius);

            if (!ground.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + 4f) ||
                Vector3.Angle(hit.normal, Vector3.up) > maxSlope)
                continue;

            CharacterController body = monsterPrefab.GetComponent<CharacterController>();
            float pivotOffset = (body.height * 0.5f - body.center.y) *
                Mathf.Abs(monsterPrefab.transform.localScale.y);
            Vector3 position = hit.point + Vector3.up * (pivotOffset + 0.12f);
            ForestMonster monster = Instantiate(monsterPrefab, position, Quaternion.identity);
            monster.BeginChase(player);
            alive.Add(monster);
            return;
        }
    }
}

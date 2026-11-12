using System.Collections.Generic;
using UnityEngine;

public class ScatterTrees : MonoBehaviour
{
    [Header("Налаштування")]
    [Tooltip("Ваші 12 префабів дерев")]
    public List<GameObject> treePrefabs;
    [Tooltip("Скільки всього дерев наліпити")]
    public int treeCount = 100;
    
    [Header("Налаштування фізики")]
    [Tooltip("Обов'язково виберіть Everything або Default")]
    public LayerMask groundLayer = ~0; // За замовчуванням вибирає "Все" (~0)

    [Header("Масштаб та Поворот")]
    [Tooltip("Масштаб тепер НЕ залежить від ландшафту і рахується в реальних метрах")]
    public float minScale = 1.0f;
    public float maxScale = 1.2f;

    [ContextMenu("Наліпити дерева")]
    public void Generate()
    {
        // 1. Шукаємо або створюємо окрему "папку" для дерев на сцені, яка не є дочірньою до масштабного ландшафту
        string folderName = $"[FOREST_TREES]_{gameObject.name}";
        GameObject forestFolder = GameObject.Find(folderName);
        
        if (forestFolder != null)
        {
            // Якщо така папка вже була, очищуємо її перед новою генерацією
            for (int i = forestFolder.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(forestFolder.transform.GetChild(i).gameObject);
            }
            Debug.Log("Видалено старі дерева з окремої папки.");
        }
        else
        {
            // Якщо папки немає, створюємо її в корені сцени зі стандартним масштабом (1,1,1)
            forestFolder = new GameObject(folderName);
            forestFolder.transform.position = Vector3.zero;
            forestFolder.transform.rotation = Quaternion.identity;
            forestFolder.transform.localScale = Vector3.one; // Масштаб 1:1:1 гарантує незалежність
        }

        // 2. Перевірка префабів
        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            Debug.LogError("СПИСОК ПРЕФАБІВ ПОРОЖНІЙ! Додайте дерева в інспекторі!");
            return;
        }

        // 3. Отримуємо колайдер ландшафту
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = GetComponentInChildren<Collider>();
        }

        if (collider == null)
        {
            Debug.LogError("НЕМАЄ КОЛАЙДЕРА! Будь ласка, додайте Mesh Collider на ваш об'єкт fbx!");
            return;
        }

        Bounds bounds = collider.bounds;
        
        Debug.Log($"Центр об'єкта: {bounds.center}, Розміри: {bounds.size}. Генерація починається...");

        int successfullySpawned = 0;
        int maxAttempts = treeCount * 10;
        int attempts = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (successfullySpawned >= treeCount) break;
            attempts++;

            // Вибираємо випадкову точку на площині
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            
            // Пускаємо промінь з високої точки вниз
            float startY = bounds.max.y + 200f;
            Vector3 rayStart = new Vector3(randomX, startY, randomZ);
            
            float rayDistance = bounds.size.y + 400f; 

            RaycastHit hit;

            if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDistance, groundLayer))
            {
                // Перевірка: чи влучили в наш ландшафт
                if (hit.collider == collider || hit.transform.IsChildOf(transform))
                {
                    GameObject randomPrefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
                    
                    if (randomPrefab == null) continue;

                    // Спавнимо дерево як дочірнє до НАШОЇ НЕЗАЛЕЖНОЇ ПАПКИ forestFolder
                    GameObject newTree = Instantiate(randomPrefab, hit.point, Quaternion.identity, forestFolder.transform);
                    newTree.name = randomPrefab.name + "(SpawnedTree)";

                    // Рандомний поворот
                    newTree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                    // Рандомний масштаб (тепер він 100% чесний, бо папка має масштаб 1,1,1)
                    float scale = Random.Range(minScale, maxScale);
                    newTree.transform.localScale = new Vector3(scale, scale, scale);

                    successfullySpawned++;
                }
            }
        }

        if (successfullySpawned == 0)
        {
            Debug.LogWarning($"Жодне дерево не було створено! Спробуйте змінити Ground Layer на 'Everything'.");
        }
        else
        {
            Debug.Log($"УСПІХ! Наліплено {successfullySpawned} дерев у незалежну папку {folderName} (спроб: {attempts}/{maxAttempts}).");
        }
    }
}
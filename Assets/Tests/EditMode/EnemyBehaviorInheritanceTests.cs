using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class EnemyBehaviorInheritanceTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void DerivedEnemies_CanBeResolvedAsEnemyBehavior()
        {
            GameObject archerObject = new GameObject("ArcherEnemy");
            createdObjects.Add(archerObject);
            SkeletonArcherEnemyBehavior archer = archerObject.AddComponent<SkeletonArcherEnemyBehavior>();

            GameObject gorgonObject = new GameObject("GorgonEnemy");
            createdObjects.Add(gorgonObject);
            GorgonEnemyBehavior gorgon = gorgonObject.AddComponent<GorgonEnemyBehavior>();

            Assert.AreSame(archer, archerObject.GetComponent<EnemyBehavior>());
            Assert.AreSame(gorgon, gorgonObject.GetComponent<EnemyBehavior>());
        }

        [Test]
        public void FindObjectsByTypeEnemyBehavior_IncludesDerivedEnemies()
        {
            EnemyBehavior melee = CreateEnemy<EnemyBehavior>("MeleeEnemy");
            EnemyBehavior archer = CreateEnemy<SkeletonArcherEnemyBehavior>("ArcherEnemy");
            EnemyBehavior gorgon = CreateEnemy<GorgonEnemyBehavior>("GorgonEnemy");

            EnemyBehavior[] allEnemies = Object.FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);

            CollectionAssert.Contains(allEnemies, melee);
            CollectionAssert.Contains(allEnemies, archer);
            CollectionAssert.Contains(allEnemies, gorgon);
        }

        private T CreateEnemy<T>(string name) where T : EnemyBehavior
        {
            GameObject enemyObject = new GameObject(name);
            createdObjects.Add(enemyObject);
            return enemyObject.AddComponent<T>();
        }
    }
}
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class StatusEffectTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayer_StatusEffects");
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PlayerMovement_ApplyPetrification_SetsPetrifiedState()
        {
            playerMovement.ApplyPetrification(5f);

            bool isPetrified = GetPrivateField<bool>(playerMovement, "isPetrified");
            int stacks = GetPrivateField<int>(playerMovement, "petrificationStacks");
            float endTime = GetPrivateField<float>(playerMovement, "petrificationEndTime");

            Assert.IsTrue(isPetrified);
            Assert.AreEqual(1, stacks);
            Assert.Greater(endTime, Time.time);
        }

        [Test]
        public void PlayerMovement_ApplyPetrification_Twice_IncreasesStacks()
        {
            playerMovement.ApplyPetrification(2f);
            playerMovement.ApplyPetrification(2f);

            int stacks = GetPrivateField<int>(playerMovement, "petrificationStacks");
            Assert.AreEqual(2, stacks);
        }

        [Test]
        public void GorgonEnemyBehavior_DefaultPetrificationDuration_IsFiveSeconds()
        {
            GameObject gorgonObject = new GameObject("TestGorgon");
            GorgonEnemyBehavior gorgon = gorgonObject.AddComponent<GorgonEnemyBehavior>();

            float petrificationDuration = GetPrivateField<float>(gorgon, "petrificationDuration");
            Assert.AreEqual(5f, petrificationDuration, 0.001f);

            Object.DestroyImmediate(gorgonObject);
        }

        [Test]
        public void GorgonEnemyBehavior_DefaultStareBoxSize_IsConfigured()
        {
            GameObject gorgonObject = new GameObject("TestGorgonBox");
            GorgonEnemyBehavior gorgon = gorgonObject.AddComponent<GorgonEnemyBehavior>();

            Vector2 stareBoxSize = GetPrivateField<Vector2>(gorgon, "stareBoxSize");
            Assert.AreEqual(new Vector2(3f, 2f), stareBoxSize);

            Object.DestroyImmediate(gorgonObject);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}

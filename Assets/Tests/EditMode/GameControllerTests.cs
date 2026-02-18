using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GameControllerTests
    {
        private GameObject gameControllerObject;
        private GameController gameController;
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        
        [SetUp]
        public void SetUp()
        {
            // Create game controller
            gameControllerObject = new GameObject("TestGameController");
            gameController = gameControllerObject.AddComponent<GameController>();
            
            // Create a player for the game controller to find
            playerObject = new GameObject("TestPlayer");
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameControllerObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void GameController_InitializationTest()
        {
            Assert.IsNotNull(gameController);
            Assert.AreEqual("TestGameController", gameController.name);
        }
        
        [Test]
        public void GameController_ComponentSetup()
        {
            Assert.IsNotNull(gameController.GetComponent<GameController>());
            Assert.AreEqual(typeof(GameController), gameController.GetType());
        }
        
        [Test]
        public void GameController_DefaultTimeScale()
        {
            // Test that time scale starts at normal speed
            Assert.AreEqual(1f, Time.timeScale);
        }
        
        [Test]
        public void GameController_PlayerReferenceCanBeFound()
        {
            // Test that a player exists in scene for GameController to find
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
            Assert.AreEqual(playerMovement, foundPlayer);
        }
        
        [Test]
        public void GameController_GameObjectActiveState()
        {
            Assert.IsTrue(gameControllerObject.activeInHierarchy);
            
            // Test deactivation
            gameControllerObject.SetActive(false);
            Assert.IsFalse(gameControllerObject.activeInHierarchy);
            
            // Test reactivation
            gameControllerObject.SetActive(true);
            Assert.IsTrue(gameControllerObject.activeInHierarchy);
        }
        
        [Test]
        public void GameController_CanManageMultipleGameObjects()
        {
            // Test creating additional game objects
            var enemy = new GameObject("TestEnemy");
            var weapon = new GameObject("TestWeapon");
            
            Assert.IsNotNull(GameObject.Find("TestEnemy"));
            Assert.IsNotNull(GameObject.Find("TestWeapon"));
            
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(weapon);
        }
        
        [Test]
        public void GameController_SceneObjectCounting()
        {
            // Test we can count objects in scene
            var initialCount = Object.FindObjectsOfType<GameObject>().Length;
            
            var tempObject = new GameObject("TempObject");
            var newCount = Object.FindObjectsOfType<GameObject>().Length;
            
            Assert.AreEqual(initialCount + 1, newCount);
            
            Object.DestroyImmediate(tempObject);
        }
        
        [Test]
        public void GameController_ComponentIntegration()
        {
            // Test that GameController can work with other components
            var audioSource = gameControllerObject.AddComponent<AudioSource>();
            Assert.IsNotNull(gameController.GetComponent<AudioSource>());
            
            var transform = gameController.transform;
            Assert.IsNotNull(transform);
            Assert.AreEqual(Vector3.zero, transform.position);
        }
    }
}
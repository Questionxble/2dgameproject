using UnityEngine;

public class NpcExampleConfigurator : MonoBehaviour
{
    private enum ExampleRole
    {
        Greeter,
        Scout
    }

    [SerializeField] private ExampleRole exampleRole = ExampleRole.Greeter;
    [SerializeField] private bool applyOnAwake = true;

    private bool hasAppliedConfiguration;

    private void Awake()
    {
        if (applyOnAwake)
        {
            ApplyConfiguration();
        }
    }

    [ContextMenu("Apply Example Configuration")]
    public void ApplyConfiguration()
    {
        if (hasAppliedConfiguration && Application.isPlaying)
        {
            return;
        }

        switch (exampleRole)
        {
            case ExampleRole.Greeter:
                ConfigureGreeter(gameObject);
                break;
            case ExampleRole.Scout:
                ConfigureScout(gameObject);
                break;
        }

        hasAppliedConfiguration = true;
    }

    private static void ConfigureGreeter(GameObject greeter)
    {
        NpcDialogueInteractable dialogueInteractable = greeter.GetComponent<NpcDialogueInteractable>();
        if (dialogueInteractable != null)
        {
            dialogueInteractable.ConfigureConversation("Sera", BuildGreeterConversation(), "start");
        }

        NpcAmbientChat ambientChat = greeter.GetComponent<NpcAmbientChat>();
        if (ambientChat != null)
        {
            ambientChat.SetResponseProfile("greeter");
        }
    }

    private static void ConfigureScout(GameObject scout)
    {
        NpcAmbientChat ambientChat = scout.GetComponent<NpcAmbientChat>();
        if (ambientChat != null)
        {
            ambientChat.SetResponseProfile("lookout");
        }

        SpriteRenderer spriteRenderer = scout.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = true;
        }
    }

    private static NpcDialogueNode[] BuildGreeterConversation()
    {
        return new[]
        {
            new NpcDialogueNode
            {
                nodeId = "start",
                speakerName = "Sera",
                lines = new[]
                {
                    "You are standing at the crossroads again. That usually means something loud is about to happen.",
                    "If you want this world to feel alive, start here and iterate from something concrete."
                },
                choices = new[]
                {
                    new NpcDialogueChoice { choiceText = "Ask what she watches for", nextNodeId = "watch" },
                    new NpcDialogueChoice { choiceText = "Ask how NPC jobs could work later", nextNodeId = "jobs" },
                    new NpcDialogueChoice { choiceText = "Leave", nextNodeId = "goodbye" }
                }
            },
            new NpcDialogueNode
            {
                nodeId = "watch",
                speakerName = "Sera",
                lines = new[]
                {
                    "Footsteps, jumps, nervous swings at empty air. The little reactions matter more than a hundred silent mannequins.",
                    "That scout down the road uses the wandering behavior. When enemies get close, it can decide whether to run or fight."
                },
                nextNodeId = "followup"
            },
            new NpcDialogueNode
            {
                nodeId = "jobs",
                speakerName = "Sera",
                lines = new[]
                {
                    "Give each NPC a role, then hang dialogue branches and state changes off that role.",
                    "Merchant, lookout, quest giver, witness. The tree can branch now, and the consequences can come later."
                },
                nextNodeId = "followup"
            },
            new NpcDialogueNode
            {
                nodeId = "followup",
                speakerName = "Sera",
                lines = new[]
                {
                    "The current setup is intentionally simple: one talkable NPC, one wandering NPC, and profile-based ambient chatter loaded from JSON."
                },
                choices = new[]
                {
                    new NpcDialogueChoice { choiceText = "That is enough for now", nextNodeId = "goodbye" },
                    new NpcDialogueChoice { choiceText = "Repeat the overview", nextNodeId = "start" }
                }
            },
            new NpcDialogueNode
            {
                nodeId = "goodbye",
                speakerName = "Sera",
                lines = new[]
                {
                    "Then go build on it. Systems get believable when they survive contact with the scene."
                },
                endConversationAfterNode = true
            }
        };
    }
}
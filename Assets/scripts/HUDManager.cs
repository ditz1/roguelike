using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;
using System;

public class HUDManager : MonoBehaviour
{

    GameObject parent_hud;
    TextMeshProUGUI player_hud_text;
    TextMeshProUGUI round_hud_text;
    TextMeshProUGUI ammo;
    TextMeshProUGUI weapon;
    TextMeshProUGUI health;
    bool game_started = false;
    bool round_text_updated = false;
    int current_round = -1;

    ZombSpawner zombSpawner;
    GameObject player_obj;
    PlayerController player;
    bool player_found = false;
    bool hud_found = false;

    // Update is called once per frame
    void Start()
    {
        zombSpawner = GameObject.Find("ZombSpawner")?.GetComponent<ZombSpawner>();
    }
    void Update()
    {

        
        if (!hud_found)
        {
            FindHudObjects();
        }
        if (!player_found && hud_found)
        {
            FindPlayer();
        }

        if (current_round != GameManager.Instance.CurrentRound)
        {
            round_text_updated = false; // Reset the flag to allow text update
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            game_started = true;
        }

        if (game_started && !round_text_updated)
        {
            UpdateRoundText();
        }

        if (Input.GetKeyDown(KeyCode.H) && game_started && zombSpawner.num_zombs_left <= 0)
        {
            GameManager.Instance.ReadyUp();
        }
        else
        {
            //Debug.Log("Cant ready up!");
        }
        if (hud_found)
        {
            UpdateAmmoText(player.curr_ammo_in_mag, player.ammo_reserve);
            health.text = player.player_hp.ToString() + "%";            
            if (weapon != null){
                weapon.text = player.weapon.name.Replace("w_", "").Replace("(Clone)", "");
            }
        }
    }

    void FindHudObjects()
    {
        if (parent_hud == null)
        {
            parent_hud = transform.Find("hud")?.gameObject;
            if (parent_hud == null)
            {
                Debug.LogError("HUD parent object not found!");
                return;
            }
        }
        if (parent_hud != null)
        {
            player_hud_text = parent_hud.transform.Find("player")?.GetComponent<TextMeshProUGUI>();
            if (player_hud_text == null)
            {
                Debug.LogError("Player HUD Text not found!");
            }
            else
            {
                player_hud_text.text = "Player: unassigned";
            }

            round_hud_text = parent_hud.transform.Find("round")?.GetComponent<TextMeshProUGUI>();
            if (round_hud_text == null)
            {
                Debug.LogError("Round HUD Text not found!");
            }
            else
            {
                round_hud_text.text = "Not Started";
            }

            ammo = parent_hud.transform.Find("ammo")?.GetComponent<TextMeshProUGUI>();
            if (ammo == null)
            {
                Debug.LogError("Current Ammo Text not found!");
            }
            else
            {
                ammo.text = "Ammo: 0";
            }

            weapon = parent_hud.transform.Find("weapon")?.GetComponent<TextMeshProUGUI>();
            if (weapon == null)
            {
                Debug.LogError("Reserve Ammo Text not found!");
            }
            else
            {
                weapon.text = "Weapon: None";
            }

            health = parent_hud.transform.Find("health")?.GetComponent<TextMeshProUGUI>();
            if (health == null)
            {
                Debug.LogError("Health Text not found!");
            }
            else
            {
                health.text = "Health: 100%";
            }
        }
        hud_found = true;
    }

    void FindPlayer()
    {
        if (player_obj == null)
        {
            player_obj = GameObject.FindGameObjectWithTag("Player");
            if (player_obj != null)
            {
                NetworkObject playerNetworkObject = player_obj.GetComponent<NetworkObject>();
                if (playerNetworkObject != null && playerNetworkObject.IsSpawned && playerNetworkObject.IsOwner)
                {
                    player_found = true;
                    // Only update text if HUD is ready
                    if (player_hud_text != null)
                    {
                        player_hud_text.text = "Player: " + player_obj.name;
                    }
                    player = player_obj.GetComponent<PlayerController>();
                    player_hud_text.text = player.player_name;
                }
            }
        }
    }

    void UpdateRoundText()
    {
        int current_round = GameManager.Instance.CurrentRound;
        if (current_round >= 0)
        {
            round_hud_text.text = "Round: " + current_round.ToString();
            round_text_updated = true; // Prevents updating the text again
        }
        else
        {
            round_hud_text.text = "Round: -1"; // Default value if round is not set
        }
    }
    void UpdateAmmoText(int current_ammo, int reserve_ammo)
    {
        if (ammo != null)
        {
            ammo.text = current_ammo + " / " + reserve_ammo;
        }
        else
        {
            Debug.LogError("Ammo Text not found!");
        }
    }
}

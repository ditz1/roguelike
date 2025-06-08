using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Bank : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] TextMeshProUGUI bank_amt;
    int bank_amount = 0;
    void Start()
    {
        bank_amount = 0;
        bank_amt.text = bank_amount.ToString();
    }

    // Update is called once per frame
    // void Update()
    // {

    // }

    public void AddToBank(int amt)
    {
        bank_amount += amt;
        bank_amt.text = bank_amount.ToString();
    }

    public int SubtractFromBank(int amt)
    {
        if (bank_amount >= amt)
        {
            bank_amount -= amt;
            bank_amt.text = bank_amount.ToString();
            return amt;
        }
        else
        {
            Debug.LogWarning("Not enough funds in the bank to subtract " + amt);
            return 0; // Not enough funds, return 0
        }
    }

}

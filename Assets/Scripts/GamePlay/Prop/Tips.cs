using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Tips : MonoBehaviour
{
    [SerializeField] private TMP_Text txt;
    public void Init(string info)
    {
        txt.text = info;
    }
}

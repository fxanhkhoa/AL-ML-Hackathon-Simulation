using UnityEngine;
using MVC.Core;
using Ezereal;
using System.Threading.Tasks;
using System;

public class CarAutoController : MonoBehaviour
{
    [Header("References")]

    [SerializeField] EzerealCarController ezerealCarController;

    async void setAccel()
    {
        ezerealCarController.setAcceleration(1f);
        await Task.Delay(TimeSpan.FromSeconds(15));
        ezerealCarController.setAcceleration(0f);
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //setAccel();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

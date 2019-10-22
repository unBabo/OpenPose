using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectBox : MonoBehaviour
{
    static public ConnectBox Instance;
    public int flag=-1;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Connect(Vector3 startpos,Vector3 endpos)
    {
        for(int i = 1; i <= 16; i++)
        {
            GameObject lineobject=GameObject.Find("LineRen"+i);
            if (lineobject.GetComponent<ConnectBox>().flag == -1)
            {
                LineRenderer line = GameObject.Find("LineRen" + i).GetComponent<LineRenderer>();
                line.SetPosition(0, startpos);
                line.SetPosition(1, endpos);

                line.startWidth = 0.1f;
                line.endWidth = 0.1f;
                lineobject.GetComponent<ConnectBox>().flag = 1;
                return;
            }
        }
       
    }

    public void FlagInit()
    {
        for(int i = 1; i <=16; i++)
        {
            GameObject.Find("LineRen" + i).GetComponent<ConnectBox>().flag = -1;
        }
        //this.GetComponent<ConnectBox>().flag = -1;


    }
}
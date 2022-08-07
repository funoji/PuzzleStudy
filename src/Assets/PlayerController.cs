using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    enum RotState
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,

        Invalid = -1,
    };
    AnimationController _animationController = new AnimationController();
    [SerializeField] PuyoController[] _puyoControllers = new PuyoController[2] { default!, default! };
    [SerializeField] BoardController boardController = default!;
    // Start is called before the first frame update

    Vector2Int _position;
    Vector2Int _last_position;
    RotState _last_rotate = RotState.Up;
    RotState _rotate = RotState.Up;
    const int TRANS_TIME = 3;
    const int ROT_TIME = 3;

    LogicalInput logical = new();
    static readonly KeyCode[] key_Code_tbl = new KeyCode[(int)LogicalInput.Key.Max]
    {
        KeyCode.RightArrow,
        KeyCode.LeftArrow,
        KeyCode.X,
        KeyCode.Z,
        KeyCode.UpArrow,
        KeyCode.DownArrow
    };

    void UpdateInput()
    {
        LogicalInput.Key inputDev = 0;

        for(int i = 0; i < (int)LogicalInput.Key.Max; i++)
        {
            if(Input.GetKey(key_Code_tbl[i]))
            {
                inputDev |= (LogicalInput.Key)(1 << i);
            }
        }
        logical.Update(inputDev);
    }
    void SetTransition(Vector2Int pos,RotState rot,int time)
    {
        _last_position = _position;
        _last_rotate = _rotate;

        _position = pos;
        _rotate = rot;

        _animationController.Set(time);
    }

    private bool Translate(bool is_right)
    {
        Vector2Int pos = _position + (is_right ? Vector2Int.right: Vector2Int. left);
        if (!CanMove(pos, _rotate)) return false;

        SetTransition(pos, _rotate, TRANS_TIME);

        return true;
    }

    static readonly Vector2Int[] rotate_tbl = new Vector2Int[]
    {
        Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left
    };

    private static Vector2Int CalcChildPuyoPos(Vector2Int pos, RotState rot)
    {
        return pos + rotate_tbl[(int)rot];
    }
    void Start()
    {
        _puyoControllers[0].SetPuyoType(PuyoType.Green);
        _puyoControllers[1].SetPuyoType(PuyoType.Red);

        _position = new Vector2Int(2, 12);
        _rotate = RotState.Up;

        _puyoControllers[0].SetPos(new Vector3((float)_position.x, (float)_position.y, 0.0f));
        Vector2Int posChild = CalcChildPuyoPos(_position, _rotate);
        _puyoControllers[1].SetPos(new Vector3((float)_position.x, (float)_position.y + 1.0f, 0.0f));
    }

    private bool CanMove(Vector2Int pos, RotState rot)
    {
        if (!boardController.CanSettle(pos)) return false;
        if (!boardController.CanSettle(CalcChildPuyoPos(pos, rot))) return false;
        return true;
    }

    //public bool Translate(bool is_right)
    //{
    //    Vector2Int pos = _position + (is_right ? Vector2Int.right : Vector2Int.left);

    //    if (!CanMove(pos, _rotate)) return false;

    //    _position = pos;

    //    _puyoControllers[0].SetPos(new Vector3((float)_position.x, (float)_position.y, 0.0f));
    //    Vector2Int posChild = CalcChildPuyoPos(_position, _rotate);
    //    _puyoControllers[1].SetPos(new Vector3((float)_position.x, (float)_position.y + 1.0f, 0.0f));
    //    return true;
    //}
    bool Rotate(bool is_right)
    {
        RotState rot = (RotState)(((int)_rotate + (is_right ? +1 : +3)) & 3);

        Vector2Int pos = _position;
        switch (rot)
        {
            case RotState.Down:
                if (!boardController.CanSettle(pos + Vector2Int.down) ||
                    !boardController.CanSettle(pos + new Vector2Int(is_right ? 1 : 1, -1)))
                {
                    pos += Vector2Int.up;
                }
                break;
            case RotState.Right:
                if (!boardController.CanSettle(pos + Vector2Int.right)) pos += Vector2Int.left;
                break;
            case RotState.Left:
                // 左: 左がうまっていれば、右に移動
                if (!boardController.CanSettle(pos + Vector2Int.left)) pos += Vector2Int.right;
                break;
            case RotState.Up:
                break;
            default:
                Debug.Assert(false);
                break;
        }

        if (!CanMove(pos, rot)) return false;

        _rotate = rot;
        _position = pos;

        _puyoControllers[0].SetPos(new Vector3((float)_position.x, (float)_position.y, 0.0f));
        Vector2Int posChild = CalcChildPuyoPos(_position, _rotate);
        _puyoControllers[1].SetPos(new Vector3((float)posChild.x, (float)posChild.y, 0.0f));

        return true;
    }
    void QuickDrop()
    {
        // 落ちれる一番下まで落ちる Vector2Int pos = _position:
        Vector2Int pos = _position;
        do
        {
            pos += Vector2Int.down;
        } while (CanMove(pos, _rotate));
        pos -= Vector2Int.down;// 一つ上の場所 (最後に置けた場所) に戻す
        _position = pos;

        bool is_set0 = boardController.Settle(_position, (int)_puyoControllers[0].GetPuyoType()); 
        Debug.Assert(is_set0);
        bool is_set1 = boardController.Settle(CalcChildPuyoPos(_position, _rotate), (int)_puyoControllers[1].GetPuyoType()); 
        Debug.Assert(is_set1);
   
        gameObject.SetActive(false);
    }

    void Control()
    {
        if (logical.IsRepeat(LogicalInput.Key.Right))
        {
            if (Translate(true)) return;
        }
        if (logical.IsRepeat(LogicalInput.Key.Left))
        {
            if (Translate(false)) return;
        }
        if (logical.IsTrigger(LogicalInput.Key.RotR))
        {
            if (Rotate(true)) return;
        }
        if (logical.IsTrigger(LogicalInput.Key.RotL))
        {
            if (Rotate(false)) return;
        }
        if (logical.IsRelease(LogicalInput.Key.QuickDrop))
        {
            QuickDrop();
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        UpdateInput();

        if (!_animationController.Update())
        {
            Control();
        }
        float anim_rate = _animationController.GetNormalized();
        _puyoControllers[0].SetPos(Interpolate(_position, RotState.Invalid, _last_position, RotState.Invalid, anim_rate));
        _puyoControllers[1].SetPos(Interpolate(_position, _rotate, _last_position, _last_rotate, anim_rate));

    }
    static Vector3 Interpolate(Vector2Int pos,RotState rot,Vector2Int pos_last ,RotState rot_last,float rate)
    {
        Vector3 p = Vector3.Lerp(
            new Vector3((float)pos.x, (float)pos.y, 0.0f),
            new Vector3((float)pos_last.x, (float)pos_last.y, 0.0f),
            rate);

        if (rot == RotState.Invalid) return p;

        float theta0  =0.5f * Mathf.PI * (float)(int)rot;
        float thetal = 0.5f * Mathf.PI * (float)(int)rot_last;
        float theta = thetal - theta0;

        if (+Mathf.PI < theta) theta = theta - 2.0f * Mathf.PI;
        if (theta < -Mathf.PI) theta = theta + 2.0f * Mathf.PI;

        theta = theta0 +rate * theta;
        return p + new Vector3(Mathf.Sin(theta), Mathf.Cos(theta), 0.0f);
    }
}

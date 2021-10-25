using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileController : MonoBehaviour
{
    private static readonly Color _selectedColor = new Color(0.5f, 0.5f, 0.5f);
    private static readonly Color _normalColor = Color.white;

    private static readonly float _moveDuration = 0.5f;
    private static readonly float destroyBigDuration = 0.1f;
    private static readonly float destroySmallDuration = 0.4f;

    private static TileController _previousSelected = null;
    private static readonly Vector2[] adjacentDirection = new Vector2[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    private static readonly Vector2 sizeBig = Vector2.one * 1.2f;
    private static readonly Vector2 sizeSmall = Vector2.zero;
    private static readonly Vector2 sizeNormal = Vector2.one;

    public int Id;
    public bool IsDestroyed { get; private set; }

    private BoardManager _board;
    private SpriteRenderer _render;
    private bool _isSelected = false;
    private GameFlowManager game;

    private void Awake()
    {
        _board = BoardManager.Instance;
        _render = GetComponent<SpriteRenderer>();
        game = GameFlowManager.Instance;
    }

    private void Start()
    {
        IsDestroyed = false;
    }

    private void OnMouseDown()
    {
        //Non Selectable Conditions
        if (_render.sprite == null || _board.IsAnimating || game.IsGameOver)
        {
            return;
        }

        SoundManager.Instance.PlayTap();
        // Already Selected this tile?
        if (_isSelected)
        {
            Deselect();
        }
        else
        {
            // if nothing selected yet
            if (_previousSelected == null)
            {
                Select();
            }
            // try swapping 
            else
            {
                // if this is an adjacent tiles
                if (GetAllAdjacentTiles().Contains(_previousSelected))
                {
                    TileController otherTile = _previousSelected;
                    _previousSelected.Deselect();
                    // swap tile
                    SwapTile(otherTile, () => {
                        if (_board.GetAllMatches().Count > 0)
                        {
                            _board.Process();
                        }
                        else
                        {
                            SoundManager.Instance.PlayWrong();
                            SwapTile(otherTile);
                        }
                    });
                }
                //if not adjacent then change selected
                else
                {
                    _previousSelected.Deselect();
                    Select();
                }
            }
        }
    }

    public void ChangeId(int id, int x, int y)
    {
        _render.sprite = _board.TileTypes[id];
        this.Id = id;

        name = "TILE_" + id + " (" + x + ", " + y + ")";
    }

    #region Select and Deselect
    private void Select()
    {
        _isSelected = true;
        _render.color = _selectedColor;
        _previousSelected = this;
    }

    private void Deselect()
    {
        _isSelected = false;
        _render.color = _normalColor;
        _previousSelected = null;
    }

    #endregion

    #region Swapping

    public void SwapTile(TileController otherTile, System.Action onCompleted = null)
    {
        StartCoroutine(_board.SwapTilePosition(this, otherTile, onCompleted));
    }

    public IEnumerator MoveTilePosition(Vector2 targetPosition, System.Action onCompleted)
    {
        Vector2 startPos = transform.position;
 
        float time = 0.0f;

        // run animation on next frame
        yield return new WaitForEndOfFrame();

        while (time < _moveDuration)
        {
            transform.position = Vector2.Lerp(startPos, targetPosition, time / _moveDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.position = targetPosition;

        onCompleted?.Invoke();
    }

    #endregion

    #region Adjacent
    private TileController GetAdjacent(Vector2 castDir)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, castDir, _render.size.x);

        if (hit)
        {
            return hit.collider.GetComponent<TileController>();
        }

        return null;
    }
    public List<TileController> GetAllAdjacentTiles()
    {
        List<TileController> adjacentTiles = new List<TileController>();

        for (int i = 0; i < adjacentDirection.Length; i++)
        {
            adjacentTiles.Add(GetAdjacent(adjacentDirection[i]));
        }

        return adjacentTiles;
    }
    #endregion

    #region Check Match
    private List<TileController> GetMatch(Vector2 castDir)
    {
        List<TileController> matchingTiles = new List<TileController>();
        RaycastHit2D hit = Physics2D.Raycast(transform.position, castDir, _render.size.x);

        while (hit)
        {
            TileController otherTile = hit.collider.GetComponent<TileController>();
            if (otherTile.Id != Id || otherTile.IsDestroyed)
            {
                break;
            }

            matchingTiles.Add(otherTile);
            hit = Physics2D.Raycast(otherTile.transform.position, castDir, _render.size.x);
        }

        return matchingTiles;
    }

    private List<TileController> GetOneLineMatch(Vector2[] paths)
    {
        List<TileController> matchingTiles = new List<TileController>();

        for (int i = 0; i < paths.Length; i++)
        {
            matchingTiles.AddRange(GetMatch(paths[i]));
        }

        // only match when more than 2 (3 with itself) in one line
        if (matchingTiles.Count >= 2)
        {
            return matchingTiles;
        }

        return null;
    }

    public List<TileController> GetAllMatches()
    {
        if (IsDestroyed)
        {
            return null;
        }

        List<TileController> matchingTiles = new List<TileController>();

        // get matches for horizontal and vertical
        List<TileController> horizontalMatchingTiles = GetOneLineMatch(new Vector2[2] { Vector2.up, Vector2.down });
        List<TileController> verticalMatchingTiles = GetOneLineMatch(new Vector2[2] { Vector2.left, Vector2.right });

        if (horizontalMatchingTiles != null)
        {
            matchingTiles.AddRange(horizontalMatchingTiles);
        }

        if (verticalMatchingTiles != null)
        {
            matchingTiles.AddRange(verticalMatchingTiles);
        }

        // add itself to matched tiles if match found
        if (matchingTiles != null && matchingTiles.Count >= 2)
        {
            matchingTiles.Add(this);
        }

        return matchingTiles;
    }
    #endregion

    #region Destroy & Generate
    public IEnumerator SetDestroyed(System.Action onCompleted)
    {
        IsDestroyed = true;
        Id = -1;
        name = "TILE_NULL";

        Vector2 startSize = transform.localScale;
        float time = 0.0f;

        while (time < destroyBigDuration)
        {
            transform.localScale = Vector2.Lerp(startSize, sizeBig, time / destroyBigDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.localScale = sizeBig;

        startSize = transform.localScale;
        time = 0.0f;

        while (time < destroySmallDuration)
        {
            transform.localScale = Vector2.Lerp(startSize, sizeSmall, time / destroySmallDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.localScale = sizeSmall;

        _render.sprite = null;

        onCompleted?.Invoke();
    }
    
    public void GenerateRandomTile(int x, int y)
    {
        transform.localScale = sizeNormal;
        IsDestroyed = false;

        ChangeId(Random.Range(0, _board.TileTypes.Count), x, y);
    }

    #endregion
}

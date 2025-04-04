using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class DungeonManager : MonoBehaviour
{
    public GameObject[] room_prefabs;
    public GameObject corridor_prefab;
    public GameObject corridor_corner;
    public GameObject starting_point; // Reference to the starting point object
    public GameObject pillar_prefab;
    public GameObject platform_prefab;
    public GameObject wall_prefab;
    public GameObject player_prefab;
    
    [Header("Dungeon Settings")]
    public int dungeonWidth = 600;
    public int dungeonHeight = 900;
    public int minRoomDistance = 40;
    public int corridorWidth = 10;
    public bool useRandomSeed = true;
    public int seed = 0;

    // this isnt the actual size of the tile prefab,
    // but rather the expected size of the prefab,
    // meaning a lower value will create more tiles and a higher value will create fewer tiles
    public float tileSize = 6.0f;
    int regen_count = 0;
    
    // Add maximum attempts configuration to prevent infinite loops
    [Header("Generation Settings")]
    public int maxRoomPlacementAttempts = 100;
    // this gridSize placement can be lower, it can also be set to specifically mimic
    // the room prefab that it is trying to place
    public int gridSizeForPlacement = 20; // Size of grid cells for room placement attempts
    
    enum Direction
    {
        NORTH,
        EAST,
        WEST,
        SOUTH,
    };

    class Room
    {
        public int x, y;         // Position
        public int width, height; // Size
        public int type;         // Room type (spawn, entry, ballroom, etc.)
        public bool placed;      // Whether this room has been placed
        public Direction entryDir;  // Direction of the entry corridor
        public Direction exitDir;   // Direction of the exit corridor
        public GameObject roomObject; // Reference to the instantiated room GameObject
        
        public Rect GetBoundsWithMargin(int margin)
        {
            return new Rect(x - margin, y - margin, width + margin * 2, height + margin * 2);
        }
    }

    class Corridor
    {
        public int fromRoom, toRoom;   
        public List<Vector2> points;   
        public GameObject corridorObject;
    }
    
    private System.Random rng;
    private List<Room> rooms = new List<Room>();
    private List<Corridor> corridors = new List<Corridor>();
    private Vector3 dungeonOrigin;
    
    void Start()
    {
        regen_count = 0;
        if (starting_point != null)
        {
            dungeonOrigin = starting_point.transform.position;
        }
        else
        {
            dungeonOrigin = transform.position;
            Debug.LogWarning("No starting point assigned, using DungeonManager's position instead.");
        }
        
        if (useRandomSeed)
        {
            // Ensure seed is always positive by using modulo or absolute value
            seed = Math.Abs((int)(DateTime.Now.Ticks % int.MaxValue));
        }
        Debug.Log("Seed: " + seed);
        rng = new System.Random(seed);
        
        // these must be kept in this order
        // init rooms, generate rooms, init corridors, generate corridors

        CreateRoomDefinitions();
        
        GenerateDungeon();
        
        InstantiateRoomsAndCorridors();

        // after generation, iterate through all tiles and find if they collide with any of the rooms
        // if the number of colliding tiles is above a certain threshold, regenerate.
        // for now, this will just regenerate the dungeon until it finds a valid one
        CheckCorridorRoomCollisions();
    }

    void InstantiatePlayer()
    {
        // get center of spawn room coordinates
        Vector3 spawnPos = ConvertToWorldPosition(rooms[0].x + rooms[0].width / 2, rooms[0].y + rooms[0].height / 2);
        spawnPos.y = 3.0f; // player will fall a bit on spawn, but need to give time for the models to load in
        GameObject player = Instantiate(player_prefab, spawnPos, Quaternion.identity);
    }

    void CheckCorridorRoomCollisions()
    {
        int collisionCount = 0;

        // Get the DungeonLayout parent from the hierarchy
        if (transform.childCount == 0)
            return;

        Transform dungeonLayout = transform.GetChild(0);

        // Iterate through all corridor parent objects
        for (int i = 0; i < dungeonLayout.childCount; i++)
        {
            Transform child = dungeonLayout.GetChild(i);

            // Check if this is a corridor parent (name starts with "Corridor_")
            if (child.name.StartsWith("Corridor_"))
            {
                // Iterate through all tiles within this corridor
                for (int j = 0; j < child.childCount; j++)
                {
                    Transform tile = child.GetChild(j);
                    Vector3 worldPos = tile.position;

                    // Convert world position back to dungeon coordinates
                    Vector2 dungeonPos = new Vector2(
                        worldPos.x - dungeonOrigin.x + dungeonWidth/2,
                        worldPos.z - dungeonOrigin.z + dungeonHeight/2
                    );

                    // Create a rect representing the tile bounds
                    Rect tileRect = new Rect(
                        dungeonPos.x - tileSize/2, 
                        dungeonPos.y - tileSize/2, 
                        tileSize, 
                        tileSize
                    );

                    // Check against all rooms
                    for (int k = 0; k < rooms.Count; k++)
                    {
                        Room room = rooms[k];
                        if (room.placed)
                        {
                            // Use a margin to check for tiles that are too close to the room
                            Rect roomRect = room.GetBoundsWithMargin(minRoomDistance / 4);

                            if (tileRect.Overlaps(roomRect))
                            {
                                // Skip if this is a tile connecting directly to this room's entry/exit
                                bool isDirectConnection = false;
                                foreach (Corridor corridor in corridors)
                                {
                                    if ((corridor.fromRoom == k || corridor.toRoom == k) && 
                                        (Vector2.Distance(dungeonPos, GetRoomDoorPosition(room, room.entryDir)) < tileSize * 3 ||
                                         Vector2.Distance(dungeonPos, GetRoomDoorPosition(room, room.exitDir)) < tileSize * 3))
                                    {
                                        isDirectConnection = true;
                                        break;
                                    }
                                }

                                if (!isDirectConnection)
                                {
                                    //Debug.LogWarning($"Tile {tile.name} overlaps with room {k} (type {room.type})");
                                    collisionCount++;

                                    if (Application.isEditor)
                                    {
                                        Renderer renderer = tile.GetComponent<Renderer>();
                                        if (renderer != null)
                                        {
                                            Material material = new Material(renderer.material);
                                            material.color = Color.red;
                                            renderer.material = material;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        Debug.Log($"Found {collisionCount} tiles overlapping with rooms");

        int collisionThreshold = 8;
        if (collisionCount > collisionThreshold)
        {
            Debug.Log($"Found {collisionCount} collisions, regenerating dungeon... (regen count: { regen_count})");
            RegenerateDungeon();
        } else {
            // Valid Stage found, instantiate player
            InstantiatePlayer();
        }
    }
    
    // Create definitions for all rooms
    void CreateRoomDefinitions()
    {
        // Clear any existing rooms
        rooms.Clear();
        
        // Define room sizes
        // not sure if these should be manually described or taken from the prefabs
        // but it might be good to have them be constant size for different room types
        int spawnSize = 20;
        int entrySize = 25;
        int ballroomSize = 70;
        int shopSize = 25;
        int exitSize = 20;
        
        // Add rooms with their types (0 = spawn, 1 = entry, etc.)
        AddRoom(spawnSize, spawnSize, 0);       // Spawn room
        AddRoom(entrySize, entrySize, 1);       // Entry room
        AddRoom(ballroomSize, ballroomSize, 2); // Ballroom
        AddRoom(shopSize, shopSize, 3);         // Shop
        AddRoom(exitSize, exitSize, 4);         // Exit
    }
    
    // Add a room of specific type and size
    void AddRoom(int width, int height, int type)
    {
        Room room = new Room();
        room.width = width;
        room.height = height;
        room.type = type;
        if (type != 2){
            room.width *= 2;
            room.height *= 2;
        }
        room.placed = false;
        // these are set by default but will be changed
        room.entryDir = Direction.NORTH;  // Default
        room.exitDir = Direction.SOUTH;   // Default
        rooms.Add(room);
    }
    
    void GenerateDungeon()
    {
        corridors.Clear();        
        rooms.Sort((a, b) => a.type.CompareTo(b.type));
        
        // Place rooms sequentially with appropriate positions
        if (!PlaceRoomsSequentially())
        {
            Debug.LogWarning("Failed to place all rooms without collisions. Try increasing dungeon size or reducing room sizes.");
            // if at this point all methods of placing rooms fail, should probably just regenerate the dungeon
            // as otherwise the rest of the mechanics will break - another thing is that if this breaks, its likely due to
            // insufficient dungeon height, so that can be auto increased as well
        }
        
        // Connect rooms with corridors
        CreateCorridors();
    }
    
    // Place rooms with vertical progression
    // Returns true if all rooms were successfully placed without collisions
    bool PlaceRoomsSequentially()
    {
        // Divide the dungeon height into sections for each room with some overlap
        float sectionHeight = dungeonHeight / (float)rooms.Count;
        bool allRoomsPlaced = true;
        // this is translated from 2d, so use of vertical really means z axis
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            
            // Calculate vertical position based on room type (progressing down)
            float verticalPos = (float)i / (rooms.Count - 1); // 0.0 for first room, 1.0 for last
            
            int midY = (int)(verticalPos * (dungeonHeight - room.height - 20) + 10);

            // test random midpoint
            //int midY = RandomRange(10, dungeonHeight - room.height - 10); 
            // Define boundaries for room placement
            int minY = midY - (int)(sectionHeight / 4);
            int maxY = midY + (int)(sectionHeight / 4);
            
            // Keep within dungeon bounds
            minY = Mathf.Max(minY, 10);
            maxY = Mathf.Min(maxY, dungeonHeight - room.height - 10);
            
            // If this is the first room (spawn room), place it at the center of the starting area
            if (i == 0)
            {
                room.x = dungeonWidth / 2 - room.width / 2;
                room.y = 10; // Place it near the top of the dungeon area
                room.placed = true;
            }
            else
            {
                // Try to place the room without overlaps
                bool validPlacement = false;
                int attempts = 0;
                
                while (!validPlacement && attempts < maxRoomPlacementAttempts)
                {
                    room.x = RandomRange(10, dungeonWidth - 20);
                    room.y = RandomRange(minY, maxY);
                    
                    validPlacement = !IsRoomOverlappingAny(room, i);
                    attempts++;
                }
                
                // If we couldn't find a valid spot with random placement, try grid-based placement
                if (!validPlacement)
                {
                    validPlacement = FindValidPositionOnGrid(room, i, minY, maxY);
                }
                
                // If we still couldn't place the room, mark it as a failure but continue
                if (!validPlacement)
                {
                    Debug.LogWarning($"Failed to place room {i} (type {room.type}) without collisions after {maxRoomPlacementAttempts} attempts.");
                    allRoomsPlaced = false;
                    
                    // Place it anyway but mark the issue
                    room.x = dungeonWidth / 2 - room.width / 2;
                    room.y = midY;
                }
                
                room.placed = true;
            }
            
            // Assign entry and exit directions - ensuring they are on different sides
            if (i == 0)
            {
                // First room only has exit
                // Set exit to SOUTH for a clear progression
                room.exitDir = Direction.SOUTH;
            }
            else if (i == rooms.Count - 1)
            {
                // Last room only has entry
                // Set entry to NORTH for a clear progression
                room.entryDir = Direction.NORTH;
            }
            else
            {
                // Middle rooms have both entry and exit
                // For clearer progression, entry is NORTH and exit is SOUTH
                // random entry, but exit must be opposite of entry
                // For middle rooms - fix the comments and make the directions more consistent
                int randomEntry = RandomRange(0, 3);
                room.entryDir = (Direction)randomEntry;
                int randomExit = RandomRange(1, 4);
                room.exitDir = (Direction)randomExit;
                if (room.entryDir == room.exitDir)
                {
                    // Ensure entry and exit are not the same
                    room.exitDir = (Direction)(((int)room.exitDir + 2) % 4);
                    if (room.exitDir == 0) room.exitDir = Direction.SOUTH;
                }
                //room.entryDir = Direction.NORTH;
                //room.exitDir = Direction.SOUTH;
            }
            
            // Update the reference in the list
            rooms[i] = room;
        }
        
        return allRoomsPlaced;
    }
    
    // Try to find a valid position for a room using a grid approach
    bool FindValidPositionOnGrid(Room room, int roomIndex, int minY, int maxY)
    {
        // Create a grid of potential positions to try
        for (int x = 50; x <= dungeonWidth - room.width - 50; x += gridSizeForPlacement)
        {
            for (int y = minY; y <= maxY; y += gridSizeForPlacement)
            {
                room.x = x;
                room.y = y;
                
                if (!IsRoomOverlappingAny(room, roomIndex))
                {
                    // Found a valid position
                    return true;
                }
            }
        }
        
        // If no valid position found, try to find the position with the least overlap
        int bestX = 50;
        int bestY = minY;
        float leastOverlapArea = float.MaxValue;
        
        for (int x = 50; x <= dungeonWidth - room.width - 50; x += gridSizeForPlacement)
        {
            for (int y = minY; y <= maxY; y += gridSizeForPlacement)
            {
                room.x = x;
                room.y = y;
                
                float overlapArea = CalculateTotalOverlapArea(room, roomIndex);
                if (overlapArea < leastOverlapArea)
                {
                    leastOverlapArea = overlapArea;
                    bestX = x;
                    bestY = y;
                }
            }
        }
        
        // Use the position with least overlap if we couldn't find a completely valid one
        room.x = bestX;
        room.y = bestY;
        
        // Return false since we couldn't find a completely valid position
        return false;
    }
    
    // Calculate the total area of overlap between a room and all previously placed rooms
    float CalculateTotalOverlapArea(Room room, int upToIndex)
    {
        float totalOverlapArea = 0;
        Rect roomRect = new Rect(room.x, room.y, room.width, room.height);

        for (int i = 0; i < upToIndex; i++)
        {
            Room placedRoom = rooms[i];
            if (placedRoom.placed)
            {
                Rect placedRect = new Rect(placedRoom.x, placedRoom.y, placedRoom.width, placedRoom.height);

                // Calculate the intersection rectangle
                if (roomRect.Overlaps(placedRect))
                {
                    // Calculate bounds of intersection
                    float xMin = Mathf.Max(roomRect.xMin, placedRect.xMin);
                    float xMax = Mathf.Min(roomRect.xMax, placedRect.xMax);
                    float yMin = Mathf.Max(roomRect.yMin, placedRect.yMin);
                    float yMax = Mathf.Min(roomRect.yMax, placedRect.yMax);

                    // Calculate area
                    float width = xMax - xMin;
                    float height = yMax - yMin;
                    totalOverlapArea += width * height;
                }
            }
        }

        return totalOverlapArea;
    }
    
    // Check if a room overlaps with any previously placed rooms
    bool IsRoomOverlappingAny(Room room, int upToIndex)
    {
        for (int i = 0; i < upToIndex; i++)
        {
            Room placedRoom = rooms[i];
            if (placedRoom.placed && CheckRoomOverlap(room, placedRoom))
            {
                return true;
            }
        }
        return false;
    }
    
    // Check if two rooms overlap or are too close
    bool CheckRoomOverlap(Room a, Room b)
    {
        // Get bounds with margin for both rooms
        Rect boundsA = a.GetBoundsWithMargin(minRoomDistance / 2);
        Rect boundsB = b.GetBoundsWithMargin(minRoomDistance / 2);
        
        // Check for overlap
        return boundsA.Overlaps(boundsB);
    }
    
    // Create corridors between rooms
    void CreateCorridors()
    {
        corridors.Clear();
        
        // Connect each room to the next one
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            ConnectRooms(i, i + 1);
        }
    }
    
    // Connect two rooms with a corridor
    void ConnectRooms(int fromIdx, int toIdx)
    {
        Room fromRoom = rooms[fromIdx];
        Room toRoom = rooms[toIdx];

        // Create a new corridor
        Corridor corridor = new Corridor();
        corridor.fromRoom = fromIdx;
        corridor.toRoom = toIdx;
        corridor.points = new List<Vector2>();

        // Calculate door positions based on room directions
        Vector2 startPos = GetRoomDoorPosition(fromRoom, fromRoom.exitDir);
        Vector2 endPos = GetRoomDoorPosition(toRoom, toRoom.entryDir);

        // Create buffer/walkway extending from exit door
        Vector2 exitBufferEnd = GetBufferEndPoint(startPos, GetOppositeDirection(fromRoom.exitDir), 4 * tileSize);

        // Create buffer/walkway leading to entry door
        Vector2 entryBufferStart = GetBufferEndPoint(endPos, GetOppositeDirection(toRoom.entryDir), 4 * tileSize);

        // Add starting point (room exit)
        corridor.points.Add(startPos);

        // Add exit buffer endpoint
        corridor.points.Add(exitBufferEnd);

        // For a SOUTH to NORTH corridor (standard progression)
        // Create a path with proper corners connecting the buffers
        float midY = (exitBufferEnd.y + entryBufferStart.y) / 2;

        // Check if buffers are roughly aligned horizontally or vertically
        bool horizontallyAligned = Mathf.Abs(exitBufferEnd.x - entryBufferStart.x) < minRoomDistance / 2;
        bool verticallyAligned = Mathf.Abs(exitBufferEnd.y - entryBufferStart.y) < minRoomDistance / 2;

        if (horizontallyAligned)
        {            
            
        }
        else if (verticallyAligned)
        {
            // If vertically aligned, use a horizontal path
            corridor.points.Add(new Vector2(exitBufferEnd.x, exitBufferEnd.y));
            corridor.points.Add(new Vector2(entryBufferStart.x, exitBufferEnd.y));
        }
        else
        {
            // Otherwise, use a z-shaped path
            corridor.points.Add(new Vector2(exitBufferEnd.x, midY));
            corridor.points.Add(new Vector2(entryBufferStart.x, midY));
        }

        // Add entry buffer start point
        corridor.points.Add(entryBufferStart);

        // Add ending point (room entry)
        corridor.points.Add(endPos);

        corridors.Add(corridor);
    }

    // Calculate buffer endpoint based on starting position and direction
    Vector2 GetBufferEndPoint(Vector2 startPos, Direction dir, float distance)
    {
        Vector2 bufferEnd = startPos;

        switch(dir)
        {
            case Direction.NORTH:
                bufferEnd.y -= distance;
                break;
            case Direction.SOUTH:
                bufferEnd.y += distance;
                break;
            case Direction.WEST:
                bufferEnd.x += distance;
                break;
            case Direction.EAST:
                bufferEnd.x -= distance;
                break;
        }

        return bufferEnd;
    }

    // Get the opposite direction
    Direction GetOppositeDirection(Direction dir)
    {
        switch(dir)
        {
            case Direction.NORTH: return Direction.SOUTH;
            case Direction.SOUTH: return Direction.NORTH;
            case Direction.EAST: return Direction.WEST;
            case Direction.WEST: return Direction.EAST;
            default: return dir;
        }
    }
    
    // Get the door position on a room for a given direction
    Vector2 GetRoomDoorPosition(Room room, Direction dir)
    {
        Vector2 pos = Vector2.zero;
        
        switch(dir)
        {
            case Direction.NORTH:
                pos.x = room.x + room.width / 2;
                pos.y = room.y;
                break;
            case Direction.SOUTH:
                pos.x = room.x + room.width / 2;
                pos.y = room.y + room.height;
                break;
            case Direction.EAST:
                pos.x = room.x + room.width;
                pos.y = room.y + room.height / 2;
                break;
            case Direction.WEST:
                pos.x = room.x;
                pos.y = room.y + room.height / 2;
                break;
        }
        
        return pos;
    }
    
    // Convert 2D position to 3D world position based on dungeon origin
    // original algorithm was written for raylib using 2d coordinate system
    // height should not be effected, x and y just mapped to x and z
    Vector3 ConvertToWorldPosition(float x, float y)
    {
        return new Vector3(
            dungeonOrigin.x + x - dungeonWidth/2,
            dungeonOrigin.y,
            dungeonOrigin.z + y - dungeonHeight/2
        );
    }

    void InstantiateCorridors()
    {

        // first, place a preemtive tile at the entrance and exit of each room
        foreach (Room room in rooms)
        {
            if (room.placed)
            {
                Vector2 doorPos_entry = GetRoomDoorPosition(room, room.entryDir);
                GameObject tile = Instantiate(corridor_corner, transform);
                tile.transform.position = ConvertToWorldPosition(doorPos_entry.x - tileSize*2.5f, doorPos_entry.y + tileSize*2.5f);
                tile.transform.localScale = new Vector3(4f, 1f, 4f);
                tile.name = $"EntryTile_{room.type}";
                Vector2 doorPos_exit = GetRoomDoorPosition(room, room.exitDir);
                GameObject tile2 = Instantiate(corridor_corner, transform);
                tile2.transform.position = ConvertToWorldPosition(doorPos_exit.x - tileSize*2.5f, doorPos_exit.y + tileSize*2.5f);
                tile2.transform.localScale = new Vector3(4f, 1f, 4f);
                tile2.name = $"ExitTile_{room.type}";
                
            }
        }

        foreach (Corridor corridor in corridors)
        {
            GameObject corridorParent = new GameObject($"Corridor_{corridor.fromRoom}_to_{corridor.toRoom}");
            corridorParent.transform.parent = transform.GetChild(0); // Attach to DungeonLayout
    
            // For each segment in the corridor
            for (int j = 0; j < corridor.points.Count - 1; ++j)
            {
                Vector2 start = corridor.points[j];
                Vector2 end = corridor.points[j + 1];
    
                // Skip zero-length segments
                if (Vector2.Distance(start, end) < 0.1f) continue;
    
                bool isVertical = Mathf.Approximately(start.x, end.x);
                bool isHorizontal = Mathf.Approximately(start.y, end.y);
                
                // Direction and length
                Vector2 direction = (end - start).normalized;
                float totalLength = Vector2.Distance(start, end);
                
                // Calculate how many tiles we need
                int tileCount = Mathf.CeilToInt(totalLength / tileSize);
                
                // Place tiles along the path
                for (int t = 0; t < tileCount; t++)
                {
                    float distanceAlongPath = t * tileSize;
                    Vector2 tilePos2D = start + direction * distanceAlongPath;
                    
                    // Skip this tile if it's inside a room (except start/end rooms)
                    bool tileInRoom = false;
                    foreach (Room room in rooms)
                    {
                        // Consider the rooms this corridor connects
                        if (room == rooms[corridor.fromRoom] || room == rooms[corridor.toRoom])
                        {
                            // Allow tiles at the edge of these rooms (for doors)
                            continue;
                        }
                        
                        // Check if tile is inside any other room
                        Rect roomBounds = new Rect(room.x, room.y, room.width, room.height);
                        if (roomBounds.Contains(tilePos2D))
                        {
                            tileInRoom = true;
                            break;
                        }
                    }
                    
                    if (tileInRoom)
                        continue;

                    GameObject tile;
                    
                    if (t <= 3 || t >= tileCount - 3){
                        tile = Instantiate(corridor_corner, corridorParent.transform);
                    } else {
                        tile = Instantiate(corridor_prefab, corridorParent.transform);
                    }
                    
                    tile.name = $"Segment_{j}_Tile_{t}";
                    if (isHorizontal){
                        if (t == 0){
                            tilePos2D.x += (tileSize * 2.25f);
                        } else {
                            tilePos2D.x += (tileSize * 2.0f);
                        }
                        tile.transform.rotation = Quaternion.Euler(0, 90, 0);
                    }
                    tile.transform.position = ConvertToWorldPosition(tilePos2D.x, tilePos2D.y);
                     
                    
                    if (!isVertical && !isHorizontal)
                    {
                        // For diagonal segments, choose closest cardinal direction
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        if (Mathf.Abs(angle) < 45 || Mathf.Abs(angle) > 135)
                        {
                            // More horizontal
                            tile.transform.rotation = Quaternion.Euler(0, 0, 0);
                        }
                        else
                        {
                            // More vertical
                            tile.transform.rotation = Quaternion.Euler(0, 0, 0);
                        }
                    }
                    
                    tile.transform.localScale = new Vector3(2.0f, 1f, 2.0f);
                }
                
                // // Add end tile for vertical segments
                // if (isVertical && j == corridor.points.Count - 2)
                // {
                //     GameObject endTile = Instantiate(corridor_prefab, corridorParent.transform);
                //     endTile.name = $"Segment_{j}_EndTile";
                //     endTile.transform.position = ConvertToWorldPosition(end.x, end.y);
                //     endTile.transform.rotation = Quaternion.Euler(0, 0, 0);
                //     endTile.transform.localScale = new Vector3(1f, 1f, 1f);
                // }
            }
        }
    }

    void InstantiateWalls(Room room)
    {
        float wall_width_offset = 4f; // higher is lower, width is divided by this
        float wall_width = room.width / wall_width_offset;
        GameObject w1 = Instantiate(wall_prefab, transform);
        w1.transform.rotation = Quaternion.Euler(0, 180, 0);
        w1.transform.position = ConvertToWorldPosition(room.x + room.width, room.y);
        w1.transform.localScale = new Vector3(wall_width, wall_width/2f, wall_width);        
        w1.name = "Wall_" + room.type + "_bottom_left";
        GameObject w2 = Instantiate(wall_prefab, transform);
        w2.transform.position = ConvertToWorldPosition(room.x, room.y + room.height);
        w2.transform.localScale = new Vector3(wall_width, wall_width/2f, wall_width);
        w2.transform.rotation = Quaternion.Euler(0, 0, 0);
        w2.name = "Wall_" + room.type + "_bottom_right";
        GameObject w3 = Instantiate(wall_prefab, transform);
        w3.transform.position = ConvertToWorldPosition(room.x + room.width, room.y + room.height);
        w3.transform.localScale = new Vector3(wall_width, wall_width/2f, wall_width);
        w3.transform.rotation = Quaternion.Euler(0, 90, 0);
        w3.name = "Wall_" + room.type + "_top_left";
        GameObject w4 = Instantiate(wall_prefab, transform);
        w4.transform.position = ConvertToWorldPosition(room.x, room.y);
        w4.transform.localScale = new Vector3(wall_width, wall_width/2f, wall_width);
        w4.transform.rotation = Quaternion.Euler(0, 270, 0);
        w4.name = "Wall_" + room.type + "_top_right";

    }
    
    void InstantiateRoomsAndCorridors()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Create a new parent for all dungeon objects
        GameObject dungeonParent = new GameObject("DungeonLayout");
        dungeonParent.transform.parent = transform;

        // Create rooms
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room.placed)
            {
                // platform for the room
                GameObject roomObject = Instantiate(platform_prefab, dungeonParent.transform);
                roomObject.transform.localScale = new Vector3(room.width, 1f, room.height);
                roomObject.transform.position = ConvertToWorldPosition(room.x + room.width/2 , room.y + room.height/2);

                InstantiateWalls(room);

                // create pillar under room
                GameObject pillar = Instantiate(pillar_prefab, dungeonParent.transform);
                pillar.transform.localScale = new Vector3(room.width, -15f, room.height);
                pillar.transform.position = ConvertToWorldPosition(room.x + room.width/2, room.y + room.height/2);
                pillar.name = "Pillar_" + room.type;

                // Store reference to the game object
                room.roomObject = roomObject;
                rooms[i] = room;
            }
        }

        // Create corridors as a separate step
        InstantiateCorridors();
    }
    
    int RandomRange(int min, int max)
    {
        return rng.Next(min, max);
    }
    
    // Public method to regenerate the dungeon (can be called from UI or other scripts)
    [ContextMenu("Regenerate Dungeon")]
    public void RegenerateDungeon()
    {
        regen_count++;
        // Update dungeon origin if starting point has moved
        if (starting_point != null)
        {
            dungeonOrigin = starting_point.transform.position;
        }

        if (useRandomSeed)
        {
            // forced positive seed, negative seeds should never occur
            seed = Math.Abs((int)(DateTime.Now.Ticks % int.MaxValue));
        }

        if (regen_count > 100){
            Debug.Log("100 tries passed, extending dungeon limits");
            dungeonHeight += 10;
        }

        Debug.Log("Regenerating dungeon with seed: " + seed);
        rng = new System.Random(seed);
        Debug.Log($"Regeneration attempt {regen_count}");

        // Clear existing data
        rooms.Clear();
        corridors.Clear();

        // Destroy ALL existing dungeon objects
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        // coroutine because sometimes the dungeon tries to regenerate and 
        // then the rooms are destroyed before the corridors are destroyed
        // race condition can be avoided by this
        StartCoroutine(RegenerationCoroutine());

    }

    private IEnumerator RegenerationCoroutine()
    {
        yield return new WaitForEndOfFrame();

        CreateRoomDefinitions();
        GenerateDungeon(); 

        Debug.Log($"Generated {rooms.Count} rooms and {corridors.Count} corridors");

        InstantiateRoomsAndCorridors();
        CheckCorridorRoomCollisions();

    }
    
    public void SetStartingPoint(GameObject newStartingPoint)
    {
        starting_point = newStartingPoint;
        if (starting_point != null)
        {
            dungeonOrigin = starting_point.transform.position;
        }
    }
    
    void OnDrawGizmos()
    {
        if (rooms == null || corridors == null || rooms.Count == 0)
            return;
        
        Vector3 gizmoOrigin = transform.position;
        if (starting_point != null)
        {
            gizmoOrigin = starting_point.transform.position;
        }
        
        // Draw dungeon bounds
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(
            new Vector3(gizmoOrigin.x, gizmoOrigin.y, gizmoOrigin.z),
            new Vector3(dungeonWidth, 5, dungeonHeight)
        );
            
        // Draw rooms as wireframe cubes
        foreach (Room room in rooms)
        {
            if (room.placed)
            {
                // Set color based on room type
                switch (room.type)
                {
                    case 0: Gizmos.color = Color.green; break;  // Spawn
                    case 1: Gizmos.color = Color.blue; break;   // Entry
                    case 2: Gizmos.color = new Color(0.5f, 0, 0.5f); break; // Ballroom
                    case 3: Gizmos.color = Color.yellow; break; // Garden
                    case 4: Gizmos.color = Color.red; break;    // Shop
                    case 5: Gizmos.color = Color.magenta; break; // Exit
                    default: Gizmos.color = Color.gray; break;
                }
                
                Vector3 roomCenter = ConvertToWorldPosition(room.x + room.width/2, room.y + room.height/2);
                
                Gizmos.DrawWireCube(roomCenter, new Vector3(room.width, 5, room.height));
                
                // Draw safety margin
                Gizmos.color = new Color(1, 1, 1, 0.2f);
                Rect bounds = room.GetBoundsWithMargin(minRoomDistance / 2);
                Vector3 marginCenter = ConvertToWorldPosition(bounds.x + bounds.width/2, bounds.y + bounds.height/2);
                Gizmos.DrawWireCube(
                    marginCenter,
                    new Vector3(bounds.width, 2, bounds.height)
                );
            }
        }
        
        // Draw corridors as lines
        Gizmos.color = Color.white;
        foreach (Corridor corridor in corridors)
        {
            for (int i = 0; i < corridor.points.Count - 1; i++)
            {
                Vector2 start = corridor.points[i];
                Vector2 end = corridor.points[i + 1];                
                Vector3 start3D = ConvertToWorldPosition(start.x, start.y);
                Vector3 end3D = ConvertToWorldPosition(end.x, end.y);
                Gizmos.DrawLine(start3D, end3D);
            }
        }
    }
}
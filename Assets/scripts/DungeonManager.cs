using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using Unity.VisualScripting;

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

    [Header("Corridor Settings")]
    public float minCorridorLength = 40f;
    public float maxCorridorLength = 120f;

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
        UpdateDungeonBounds();
        
        InstantiateRoomsAndCorridors();

        // after generation, iterate through all tiles and find if they collide with any of the rooms
        // if the number of colliding tiles is above a certain threshold, regenerate.
        // for now, this will just regenerate the dungeon until it finds a valid one
        CheckCorridorRoomCollisions();
    }

    void InstantiatePlayer()
    {
        Destroy(GameObject.FindWithTag("Player"));

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
        bool allRoomsPlaced = true;
        
        // Place spawn room first at the predetermined starting position
        Room spawnRoom = rooms[0];
        spawnRoom.x = dungeonWidth / 2 - spawnRoom.width / 2;
        spawnRoom.y = dungeonHeight / 2 - spawnRoom.height / 2;
        spawnRoom.placed = true;
        rooms[0] = spawnRoom;
        
        // Place remaining rooms with constrained distances from previous rooms
        for (int i = 1; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            Room previousRoom = rooms[i-1];
            
            // Try to place the room without overlaps and within corridor constraints
            bool validPlacement = false;
            int attempts = 0;
            
            while (!validPlacement && attempts < maxRoomPlacementAttempts)
            {
                // Generate a random distance between min and max corridor length
                float corridorLength = RandomRange((int)minCorridorLength, (int)maxCorridorLength);
                
                // Generate a random angle (in radians) for placement
                float angle = (float)RandomRange(0, 360) * Mathf.Deg2Rad;
                
                // Calculate position based on distance and angle from previous room's center
                Vector2 prevRoomCenter = new Vector2(
                    previousRoom.x + previousRoom.width / 2,
                    previousRoom.y + previousRoom.height / 2
                );
                
                // Calculate new room position (top-left corner)
                int newX = (int)(prevRoomCenter.x + Mathf.Cos(angle) * corridorLength - room.width / 2);
                int newY = (int)(prevRoomCenter.y + Mathf.Sin(angle) * corridorLength - room.height / 2);
                
                // Keep the room within dungeon bounds
                newX = Mathf.Clamp(newX, 10, dungeonWidth - room.width - 10);
                newY = Mathf.Clamp(newY, 10, dungeonHeight - room.height - 10);
                
                room.x = newX;
                room.y = newY;
                
                // Check if the room is at an acceptable distance from all previous rooms
                bool validDistance = true;
                for (int j = 0; j < i; j++)
                {
                    Room otherRoom = rooms[j];
                    if (otherRoom.placed)
                    {
                        // Calculate center-to-center distance
                        Vector2 otherCenter = new Vector2(
                            otherRoom.x + otherRoom.width / 2,
                            otherRoom.y + otherRoom.height / 2
                        );
                        
                        Vector2 currentCenter = new Vector2(
                            room.x + room.width / 2,
                            room.y + room.height / 2
                        );
                        
                        float distanceBetweenRooms = Vector2.Distance(currentCenter, otherCenter);
                        
                        // For previous room, ensure distance is within corridor constraints
                        if (j == i - 1)
                        {
                            if (distanceBetweenRooms < minCorridorLength || distanceBetweenRooms > maxCorridorLength)
                            {
                                validDistance = false;
                                break;
                            }
                        }
                        // For all other rooms, just ensure no overlap
                        else if (CheckRoomOverlap(room, otherRoom))
                        {
                            validDistance = false;
                            break;
                        }
                    }
                }
                
                validPlacement = validDistance;
                attempts++;
            }
            
            // If we couldn't find a valid spot with random placement, try grid-based placement
            if (!validPlacement)
            {
                validPlacement = FindValidPositionWithCorridorConstraints(room, i, minCorridorLength, maxCorridorLength);
            }
            
            // If we still couldn't place the room, place it at minimum distance in a random direction
            if (!validPlacement)
            {
                Debug.LogWarning($"Failed to place room {i} (type {room.type}) with corridor constraints after {maxRoomPlacementAttempts} attempts.");
                allRoomsPlaced = false;
                
                // Place it at minimum distance in a random direction
                previousRoom = rooms[i-1];
                float angle = (float)RandomRange(0, 360) * Mathf.Deg2Rad;
                
                Vector2 prevRoomCenter = new Vector2(
                    previousRoom.x + previousRoom.width / 2,
                    previousRoom.y + previousRoom.height / 2
                );
                
                int newX = (int)(prevRoomCenter.x + Mathf.Cos(angle) * minCorridorLength - room.width / 2);
                int newY = (int)(prevRoomCenter.y + Mathf.Sin(angle) * minCorridorLength - room.height / 2);
                
                // Keep the room within dungeon bounds
                room.x = Mathf.Clamp(newX, 10, dungeonWidth - room.width - 10);
                room.y = Mathf.Clamp(newY, 10, dungeonHeight - room.height - 10);
            }
            
            room.placed = true;
            
            // Set entry and exit directions based on relative positions from previous room
            AssignRoomDirections(room, previousRoom);
            
            // Update the reference in the list
            rooms[i] = room;
        }
        
        return allRoomsPlaced;
    }
    
    // New method to find a valid position with corridor length constraints
    bool FindValidPositionWithCorridorConstraints(Room room, int roomIndex, float minLength, float maxLength)
    {
        Room previousRoom = rooms[roomIndex - 1];
        Vector2 prevRoomCenter = new Vector2(
            previousRoom.x + previousRoom.width / 2,
            previousRoom.y + previousRoom.height / 2
        );
        
        // Try different angles in smaller increments
        for (int angleStep = 0; angleStep < 36; angleStep++)
        {
            float angle = (float)angleStep * 10f * Mathf.Deg2Rad;
            
            // Try different distances between min and max
            for (float distance = minLength; distance <= maxLength; distance += minLength / 2)
            {
                int newX = (int)(prevRoomCenter.x + Mathf.Cos(angle) * distance - room.width / 2);
                int newY = (int)(prevRoomCenter.y + Mathf.Sin(angle) * distance - room.height / 2);
                
                // Keep the room within dungeon bounds
                newX = Mathf.Clamp(newX, 10, dungeonWidth - room.width - 10);
                newY = Mathf.Clamp(newY, 10, dungeonHeight - room.height - 10);
                
                room.x = newX;
                room.y = newY;
                
                // Check if this placement is valid
                bool validPlacement = true;
                for (int j = 0; j < roomIndex; j++)
                {
                    Room otherRoom = rooms[j];
                    if (otherRoom.placed)
                    {
                        // For previous room, check distance constraints
                        if (j == roomIndex - 1)
                        {
                            Vector2 otherCenter = new Vector2(
                                otherRoom.x + otherRoom.width / 2,
                                otherRoom.y + otherRoom.height / 2
                            );
                            
                            Vector2 currentCenter = new Vector2(
                                room.x + room.width / 2,
                                room.y + room.height / 2
                            );
                            
                            float distanceBetweenRooms = Vector2.Distance(currentCenter, otherCenter);
                            
                            if (distanceBetweenRooms < minLength || distanceBetweenRooms > maxLength)
                            {
                                validPlacement = false;
                                break;
                            }
                        }
                        // For all other rooms, just ensure no overlap
                        else if (CheckRoomOverlap(room, otherRoom))
                        {
                            validPlacement = false;
                            break;
                        }
                    }
                }
                
                if (validPlacement)
                {
                    return true;
                }
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

        // Add starting point (room exit)
        corridor.points.Add(startPos);

        // Create buffer/walkway extending from exit door
        Vector2 exitBufferEnd = GetBufferEndPoint(startPos, fromRoom.exitDir, 3 * tileSize);
        corridor.points.Add(exitBufferEnd);

        // Create buffer/walkway leading to entry door
        Vector2 entryBufferStart = GetBufferEndPoint(endPos, toRoom.entryDir, 3 * tileSize);

        // Only create straight corridors with right-angle turns
        // First, determine if we need to make a turn
        bool needsHorizontalFirst = ShouldGoHorizontalFirst(fromRoom.exitDir, toRoom.entryDir);

        if (needsHorizontalFirst)
        {
            // Go horizontal first, then vertical
            Vector2 cornerPoint = new Vector2(entryBufferStart.x, exitBufferEnd.y);
            corridor.points.Add(cornerPoint);
        }
        else
        {
            // Go vertical first, then horizontal
            Vector2 cornerPoint = new Vector2(exitBufferEnd.x, entryBufferStart.y);
            corridor.points.Add(cornerPoint);
        }

        // Add entry buffer start point
        corridor.points.Add(entryBufferStart);

        // Add ending point (room entry)
        corridor.points.Add(endPos);

        corridors.Add(corridor);
    }

    // Helper method to determine if we should go horizontal first
    bool ShouldGoHorizontalFirst(Direction exitDir, Direction entryDir)
    {
        // If exit is East/West, go horizontal first
        if (exitDir == Direction.EAST || exitDir == Direction.WEST)
            return true;

        // If exit is North/South, go vertical first
        if (exitDir == Direction.NORTH || exitDir == Direction.SOUTH)
            return false;

        // Default to horizontal first
        return true;
    }

    // New method to calculate a good corner point for corridors
    void AssignRoomDirections(Room current, Room previous)
    {
        // Get centers of both rooms
        Vector2 currentCenter = new Vector2(current.x + current.width / 2, current.y + current.height / 2);
        Vector2 previousCenter = new Vector2(previous.x + previous.width / 2, previous.y + previous.height / 2);

        // Calculate horizontal and vertical differences
        float xDiff = currentCenter.x - previousCenter.x;
        float yDiff = currentCenter.y - previousCenter.y;

        // Determine if the connection should be horizontal or vertical
        bool connectHorizontally = Mathf.Abs(xDiff) >= Mathf.Abs(yDiff);

        if (connectHorizontally)
        {
            // Connect horizontally
            if (xDiff > 0)
            {
                previous.exitDir = Direction.EAST;
                current.entryDir = Direction.WEST;
            }
            else
            {
                previous.exitDir = Direction.WEST;
                current.entryDir = Direction.EAST;
            }
        }
        else
        {
            // Connect vertically
            if (yDiff > 0)
            {
                previous.exitDir = Direction.SOUTH;
                current.entryDir = Direction.NORTH;
            }
            else
            {
                previous.exitDir = Direction.NORTH;
                current.entryDir = Direction.SOUTH;
            }
        }

        // Set a random exit direction for current room if it's not the last room
        if (current.type != rooms.Count - 1)
        {
            List<Direction> possibleDirections = new List<Direction>
            {
                Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST
            };
            possibleDirections.Remove(current.entryDir);
            current.exitDir = possibleDirections[RandomRange(0, possibleDirections.Count)];
        }
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
                tile.transform.Translate(0, 0.05f, 0);
                tile.transform.localScale = new Vector3(4f, 3f, 4f);
                tile.name = $"EntryTile_{room.type}";
                Vector2 doorPos_exit = GetRoomDoorPosition(room, room.exitDir);
                GameObject tile2 = Instantiate(corridor_corner, transform);
                tile2.transform.position = ConvertToWorldPosition(doorPos_exit.x - tileSize*2.5f, doorPos_exit.y + tileSize*2.5f);
                tile.transform.Translate(0, 0.05f, 0);
                tile2.transform.localScale = new Vector3(4f, 3f, 4f);
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

                // Skip diagonal segments (should not happen with the new generation)
                if (!isVertical && !isHorizontal)
                {
                    Debug.LogWarning("Diagonal corridor segment detected! Should not happen with straight corridors only.");
                    continue;
                }

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

                    // Choose tile prefab based on position in the corridor
                    GameObject tile;
                    if (t <= 3 || t >= tileCount - 3)
                    {
                        tile = Instantiate(corridor_corner, corridorParent.transform);
                    }
                    else
                    {
                        tile = Instantiate(corridor_prefab, corridorParent.transform);
                    }

                    tile.name = $"Segment_{j}_Tile_{t}";

                    // Set position and rotation
                    if (isHorizontal)
                    {
                        // Adjust horizontal corridors
                        if (t == 0)
                        {
                            tilePos2D.x += tileSize * 2.25f;
                        }
                        else
                        {
                            tilePos2D.x += tileSize * 2.0f;
                        }
                        tile.transform.rotation = Quaternion.Euler(0, 90, 0);
                    }
                    else
                    {
                        // Vertical corridors don't need special rotation
                        tile.transform.rotation = Quaternion.Euler(0, 0, 0);
                    }

                    tile.transform.position = ConvertToWorldPosition(tilePos2D.x, tilePos2D.y);

                    if (t <= 3 || t >= tileCount - 3)
                    {
                        tile.transform.Translate(0, 0.15f, 0);
                    }

                    tile.transform.localScale = new Vector3(2.0f, 3f, 2.0f);
                }
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

    void UpdateDungeonBounds()
    {
        // Start with the minimum required size for the spawn room
        int minX = rooms[0].x;
        int maxX = rooms[0].x + rooms[0].width;
        int minY = rooms[0].y;
        int maxY = rooms[0].y + rooms[0].height;

        // Expand bounds to include all placed rooms plus a margin
        int margin = minRoomDistance * 2;
        foreach (Room room in rooms)
        {
            if (room.placed)
            {
                minX = Mathf.Min(minX, room.x - margin);
                maxX = Mathf.Max(maxX, room.x + room.width + margin);
                minY = Mathf.Min(minY, room.y - margin);
                maxY = Mathf.Max(maxY, room.y + room.height + margin);
            }
        }

        // Update dungeon dimensions
        dungeonWidth = maxX - minX;
        dungeonHeight = maxY - minY;

        // Adjust room positions relative to new origin
        int offsetX = -minX;
        int offsetY = -minY;

        foreach (Room room in rooms)
        {
            if (room.placed)
            {
                room.x += offsetX;
                room.y += offsetY;
            }
        }

        // Adjust corridor points
        foreach (Corridor corridor in corridors)
        {
            for (int i = 0; i < corridor.points.Count; i++)
            {
                Vector2 point = corridor.points[i];
                corridor.points[i] = new Vector2(point.x + offsetX, point.y + offsetY);
            }
        }
    }
    
    int RandomRange(int min, int max)
    {
        return rng.Next(min, max);
    }
    
    // Public method to regenerate the dungeon (can be called from UI or other scripts)
    [ContextMenu("Regenerate Dungeon")]
    public void RegenerateDungeon()
    {
        Destroy(GameObject.FindWithTag("Player"));
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

        if (regen_count % 20 == 0){
            Debug.Log("20 tries passed, extending dungeon limits");
            dungeonHeight += 20;
            dungeonWidth += 20;

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
        Destroy(GameObject.FindWithTag("Player"));

        CreateRoomDefinitions();
        GenerateDungeon(); 
        UpdateDungeonBounds();

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
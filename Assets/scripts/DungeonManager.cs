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
    public GameObject checkpointManagerPrefab;
    
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
        
        RegenerateDungeon();
    }

    void InstantiatePlayer()
    {
        // get center of spawn room coordinates
        if (GameObject.Find("Player") == null)
        {
            Vector3 spawnPos = ConvertToWorldPosition(rooms[0].x + rooms[0].width / 2, rooms[0].y + rooms[0].height / 2);
            spawnPos.y = 3.0f; // player will fall a bit on spawn, but need to give time for the models to load in
            GameObject player = Instantiate(player_prefab, spawnPos, Quaternion.identity);
        } else {
            GameObject player = GameObject.Find("Player");
            Vector3 spawnPos = ConvertToWorldPosition(rooms[0].x + rooms[0].width / 2, rooms[0].y + rooms[0].height / 2);
            spawnPos.y = 3.0f; // player will fall a bit on spawn, but need to give time for the models to load in
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.identity;
        }
       
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
            Debug.Log($"Found {collisionCount} collisions, regenerating dungeon... (regen count: {regen_count})");
            RegenerateDungeon();
        } else {
            // Valid Stage found, instantiate player
            Destroy(GameObject.FindWithTag("Player"));
            InstantiatePlayer();
        }
    }
    
    // Create definitions for all rooms
    void CreateRoomDefinitions()
    {
        // Clear any existing rooms
        rooms.Clear();

        List<int> room_sizes = new List<int>();
        room_sizes.Add(20); // Spawn room
        room_sizes.Add(25); // Entry room
        room_sizes.Add(70); // Ballroom
        room_sizes.Add(25); // Shop
        //room_sizes.Add(25); // Boss room
        //room_sizes.Add(30);
        room_sizes.Add(20); // Exit room
        

        for (int i = 0; i < room_sizes.Count; i++)
        {
            int roomSize = room_sizes[i];
            int roomType = i;
            AddRoom(roomSize, roomSize, roomType);
        }
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
        }
        
        // Connect rooms with corridors
        CreateCorridors();
    }
    
    // Place rooms with progressive arrangement
    bool PlaceRoomsSequentially()
    {
        bool allRoomsPlaced = true;

        // Place spawn room first at a fixed position
        Room spawnRoom = rooms[0];
        spawnRoom.x = dungeonWidth / 2 - spawnRoom.width / 2;
        spawnRoom.y = 10; // Near the top of the dungeon
        spawnRoom.placed = true;
        spawnRoom.exitDir = Direction.SOUTH; // Spawn room's exit is south
        rooms[0] = spawnRoom;

        // Place each subsequent room directly aligned with the previous room's exit
        for (int i = 1; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            Room previousRoom = rooms[i-1];

            // Set entry direction (opposite of previous room's exit)
            room.entryDir = GetOppositeDirection(previousRoom.exitDir);

            // Calculate door positions for alignment
            Vector2 previousDoor = GetRoomDoorPosition(previousRoom, previousRoom.exitDir);

            // Calculate a random distance for the corridor
            float corridorLength = RandomRange((int)minCorridorLength, (int)maxCorridorLength);

            // Position the room directly aligned with the exit direction
            Vector2 newRoomPos = Vector2.zero;

            switch (previousRoom.exitDir)
            {
                case Direction.NORTH:
                    // Previous exit is North, so new room entry is South
                    newRoomPos.x = previousDoor.x - room.width / 2; 
                    newRoomPos.y = previousDoor.y - corridorLength - room.height;
                    break;

                case Direction.SOUTH:
                    // Previous exit is South, so new room entry is North
                    newRoomPos.x = previousDoor.x - room.width / 2; 
                    newRoomPos.y = previousDoor.y + corridorLength;
                    break;

                case Direction.EAST:
                    // Previous exit is East, so new room entry is West
                    newRoomPos.x = previousDoor.x + corridorLength; 
                    newRoomPos.y = previousDoor.y - room.height / 2; 
                    break;

                case Direction.WEST:
                    // Previous exit is West, so new room entry is East
                    newRoomPos.x = previousDoor.x - corridorLength - room.width;
                    newRoomPos.y = previousDoor.y - room.height / 2; 
                    break;
            }

            // Set the room position
            room.x = (int)newRoomPos.x;
            room.y = (int)newRoomPos.y;

            // Keep the room within dungeon bounds
            room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
            room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

            // Check for collisions with previous rooms
            bool hasCollision = IsRoomOverlappingAny(room, i);
            int collisionResolveAttempts = 0;
            const int maxCollisionResolveAttempts = 10;

            // Try to resolve collisions by adjusting corridor length
            while (hasCollision && collisionResolveAttempts < maxCollisionResolveAttempts)
            {
                // Increase corridor length to move the room further away
                corridorLength += minRoomDistance;

                // Recalculate position with new corridor length
                switch (previousRoom.exitDir)
                {
                    case Direction.NORTH:
                        newRoomPos.y = previousDoor.y - corridorLength - room.height;
                        break;

                    case Direction.SOUTH:
                        newRoomPos.y = previousDoor.y + corridorLength;
                        break;

                    case Direction.EAST:
                        newRoomPos.x = previousDoor.x + corridorLength;
                        break;

                    case Direction.WEST:
                        newRoomPos.x = previousDoor.x - corridorLength - room.width;
                        break;
                }

                // Update room position
                room.x = (int)newRoomPos.x;
                room.y = (int)newRoomPos.y;

                // Keep the room within dungeon bounds
                room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
                room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

                // Check if collision is resolved
                hasCollision = IsRoomOverlappingAny(room, i);
                collisionResolveAttempts++;
            }

            // If we still have collisions, try a different exit direction
            if (hasCollision)
            {
                List<Direction> availableDirections = new List<Direction>
                {
                    Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST
                };

                availableDirections.Remove(room.entryDir); // Can't exit in the entry direction

                bool foundValidDirection = false;

                // Try each available direction
                foreach (Direction newExitDir in availableDirections)
                {
                    previousRoom.exitDir = newExitDir;
                    room.entryDir = GetOppositeDirection(previousRoom.exitDir);

                    // Recalculate door position
                    previousDoor = GetRoomDoorPosition(previousRoom, previousRoom.exitDir);
                    corridorLength = RandomRange((int)minCorridorLength, (int)maxCorridorLength);

                    // Recalculate room position
                    switch (previousRoom.exitDir)
                    {
                        case Direction.NORTH:
                            newRoomPos.x = previousDoor.x - room.width / 2;
                            newRoomPos.y = previousDoor.y - corridorLength - room.height;
                            break;

                        case Direction.SOUTH:
                            newRoomPos.x = previousDoor.x - room.width / 2;
                            newRoomPos.y = previousDoor.y + corridorLength;
                            break;

                        case Direction.EAST:
                            newRoomPos.x = previousDoor.x + corridorLength;
                            newRoomPos.y = previousDoor.y - room.height / 2;
                            break;

                        case Direction.WEST:
                            newRoomPos.x = previousDoor.x - corridorLength - room.width;
                            newRoomPos.y = previousDoor.y - room.height / 2;
                            break;
                    }

                    // Update room position
                    room.x = (int)newRoomPos.x;
                    room.y = (int)newRoomPos.y;

                    // Keep the room within dungeon bounds
                    room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
                    room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

                    // Check if this direction works
                    if (!IsRoomOverlappingAny(room, i))
                    {
                        foundValidDirection = true;
                        break;
                    }
                }

                // If all directions fail, force placement by increasing distance dramatically
                if (!foundValidDirection)
                {
                    Debug.LogWarning($"Could not place room {i} without collisions. Using fallback placement.");

                    corridorLength = maxCorridorLength * 2;

                    // Recalculate room position with extreme distance
                    switch (previousRoom.exitDir)
                    {
                        case Direction.NORTH:
                            newRoomPos.x = previousDoor.x - room.width / 2;
                            newRoomPos.y = previousDoor.y - corridorLength - room.height;
                            break;

                        case Direction.SOUTH:
                            newRoomPos.x = previousDoor.x - room.width / 2;
                            newRoomPos.y = previousDoor.y + corridorLength;
                            break;

                        case Direction.EAST:
                            newRoomPos.x = previousDoor.x + corridorLength;
                            newRoomPos.y = previousDoor.y - room.height / 2;
                            break;

                        case Direction.WEST:
                            newRoomPos.x = previousDoor.x - corridorLength - room.width;
                            newRoomPos.y = previousDoor.y - room.height / 2;
                            break;
                    }

                    room.x = (int)newRoomPos.x;
                    room.y = (int)newRoomPos.y;

                    // Ensure room stays within dungeon bounds
                    room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
                    room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

                    allRoomsPlaced = false;
                }

                // Update previous room in the list to reflect its new exit direction
                rooms[i-1] = previousRoom;
            }

            // Choose a random exit direction for this room (different from entry)
            if (i < rooms.Count - 1)  // Not the last room
            {
                List<Direction> possibleExitDirs = new List<Direction> { 
                    Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST 
                };
                possibleExitDirs.Remove(room.entryDir);
                room.exitDir = possibleExitDirs[RandomRange(0, possibleExitDirs.Count)];

                // cannot allow to have the same direction exit 3 times in a row
                if (i > 2 && room.exitDir == rooms[i-1].exitDir && room.exitDir == rooms[i-2].exitDir)
                {
                    // Force a different exit direction
                    possibleExitDirs.Remove(room.exitDir);
                    room.exitDir = possibleExitDirs[RandomRange(0, possibleExitDirs.Count)];
                }
            }

            

            room.placed = true;
            rooms[i] = room;
        }

        return allRoomsPlaced;
    }


    // Simplify corridor creation to use straight paths
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

        // Add ending point (room entry)
        corridor.points.Add(endPos);

        corridors.Add(corridor);
    }

    // Try to resolve room overlap by gradually moving the room away from overlapping rooms
    bool TryResolveRoomOverlap(Room room, int roomIndex)
    {
        // Initial step size for movement
        int stepSize = 10;
        int maxSteps = 10; // Maximum number of steps to try

        // Try moving in different directions
        Direction[] moveDirections = { Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST };

        foreach (Direction moveDir in moveDirections)
        {
            // Try moving stepSize units at a time
            for (int step = 1; step <= maxSteps; step++)
            {
                int moveAmount = step * stepSize;

                // Original position
                int originalX = room.x;
                int originalY = room.y;

                // Move room
                switch (moveDir)
                {
                    case Direction.NORTH:
                        room.y -= moveAmount;
                        break;
                    case Direction.SOUTH:
                        room.y += moveAmount;
                        break;
                    case Direction.EAST:
                        room.x += moveAmount;
                        break;
                    case Direction.WEST:
                        room.x -= moveAmount;
                        break;
                }

                // Keep the room within dungeon bounds
                room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
                room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

                // Check if this new position resolves the overlap
                if (!IsRoomOverlappingAny(room, roomIndex))
                {
                    return true; // Found a valid position
                }

                // Restore original position for next attempt
                room.x = originalX;
                room.y = originalY;
            }
        }

        return false; // Could not resolve overlap
    }

    // Force resolution of room overlap by moving far enough away from all other rooms
    void ForceResolveRoomOverlap(Room room, int roomIndex)
    {
        // Find the average center position of all existing rooms
        Vector2 averageCenter = Vector2.zero;
        int placedRoomCount = 0;

        for (int i = 0; i < roomIndex; i++)
        {
            if (rooms[i].placed)
            {
                averageCenter.x += rooms[i].x + rooms[i].width / 2;
                averageCenter.y += rooms[i].y + rooms[i].height / 2;
                placedRoomCount++;
            }
        }

        if (placedRoomCount > 0)
        {
            averageCenter /= placedRoomCount;
        }

        // Find a direction away from the average center
        Vector2 roomCenter = new Vector2(room.x + room.width / 2, room.y + room.height / 2);
        Vector2 directionVector = (roomCenter - averageCenter).normalized;

        // Determine a large enough distance to avoid overlap
        float minDistanceNeeded = minRoomDistance * 2;

        // Keep moving outward until no overlap is found or we reach the dungeon boundary
        bool validPlacement = false;
        for (float distance = minDistanceNeeded; distance < dungeonWidth && !validPlacement; distance += minRoomDistance)
        {
            Vector2 newCenter = averageCenter + directionVector * distance;

            // Convert from center position to top-left position
            room.x = (int)(newCenter.x - room.width / 2);
            room.y = (int)(newCenter.y - room.height / 2);

            // Keep the room within dungeon bounds
            room.x = Mathf.Clamp(room.x, 10, dungeonWidth - room.width - 10);
            room.y = Mathf.Clamp(room.y, 10, dungeonHeight - room.height - 10);

            validPlacement = !IsRoomOverlappingAny(room, roomIndex);

            // If we still can't find a non-overlapping position, we'll just take the last position
            if (distance > dungeonWidth / 2)
            {
                Debug.LogWarning($"Force placed room {roomIndex} at edge of dungeon due to persistent overlap.");
                break;
            }
        }
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
        // Place tiles at the entrance and exit of each room
        foreach (Room room in rooms)
        {
            if (room.placed)
            {
                // Create entry door tile if this isn't the first room
                if (room.type != 0)
                {
                    Vector2 doorPos_entry = GetRoomDoorPosition(room, room.entryDir);
                    GameObject tile = Instantiate(corridor_corner, transform);
                    tile.transform.position = ConvertToWorldPosition(doorPos_entry.x - tileSize*2.0f, doorPos_entry.y + tileSize*2.0f);
                    tile.transform.Translate(0, 0.2f, 0);
                    tile.transform.localScale = new Vector3(4f, 1f, 4f);
                    tile.name = $"EntryTile_{room.type}_{room.entryDir}";
                }
                
                // Create exit door tile if this isn't the last room
                if (room.type != rooms.Count - 1)
                {
                    Vector2 doorPos_exit = GetRoomDoorPosition(room, room.exitDir);
                    GameObject tile2 = Instantiate(corridor_corner, transform);
                    tile2.transform.position = ConvertToWorldPosition(doorPos_exit.x - tileSize*2.0f, doorPos_exit.y + tileSize*2.0f);
                    tile2.transform.Translate(0, 0.2f, 0);
                    tile2.transform.localScale = new Vector3(4f, 1f, 4f);
                    tile2.name = $"ExitTile_{room.type}_{room.exitDir}";
                }
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
                
                // Skip diagonal segments (shouldn't happen with this generation)
                if (!isVertical && !isHorizontal)
                {
                    Debug.LogWarning("Diagonal corridor segment detected at segment " + j);
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
                    
                    // Skip tiles inside rooms except connector tiles
                    bool tileInRoom = false;
                    foreach (Room room in rooms)
                    {
                        if (room == rooms[corridor.fromRoom] || room == rooms[corridor.toRoom])
                        {
                            // Allow tiles at the edge of these rooms (for doors)
                            continue;
                        }
                        
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
                    
                    // Use corner tiles at the ends of segments
                    if (t <= 1 || t >= tileCount - 1)
                    {
                        tile = Instantiate(corridor_corner, corridorParent.transform);
                    } 
                    else 
                    {
                        tile = Instantiate(corridor_prefab, corridorParent.transform);
                    }
                    
                    tile.name = $"Segment_{j}_Tile_{t}";
                    
                   if (isHorizontal)
                    {
                        tile.transform.rotation = Quaternion.Euler(0, 90, 0);
                        tilePos2D.y += tileSize * 1.25f;
                        tilePos2D.x += tileSize * 1.25f;
                    }
                    if (isVertical)
                    {
                        tile.transform.rotation = Quaternion.Euler(0, 0, 0);
                        tilePos2D.x -= tileSize * 1.25f;
                        tilePos2D.y += tileSize * 1.25f;
                    }
                    
                    tile.transform.position = ConvertToWorldPosition(tilePos2D.x, tilePos2D.y);
                    
                   
                    if (t <= 1 || t >= tileCount - 1)
                    {
                        tile.transform.Translate(0, 0.1f, 0);
                    }
                    
                    tile.transform.localScale = new Vector3(2.5f, 5.0f, 2.5f);
                }
            }
        }
    }

    void InstantiateWalls(Room room)
    {
        float wall_width_offset = 4f; 
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
                roomObject.transform.position = ConvertToWorldPosition(room.x + room.width/2, room.y + room.height/2);

                InstantiateWalls(room);

                // create pillar under room
                GameObject pillar = Instantiate(pillar_prefab, dungeonParent.transform);
                pillar.transform.position = ConvertToWorldPosition(room.x + room.width/2, room.y + room.height/2);
                pillar.transform.localScale = new Vector3(room.width * 1.25f, -15f, room.height * 1.25f);
                pillar.name = "Pillar_" + room.type;

                // Store reference to the game object
                room.roomObject = roomObject;
                rooms[i] = room;
            }
        }

        // Create corridors as a separate step
        InstantiateCorridors();

        SpawnCheckpointManager();
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
        
        StartCoroutine(RegenerationCoroutine());
    }

    private IEnumerator RegenerationCoroutine()
    {
        yield return new WaitForEndOfFrame();

        CreateRoomDefinitions();
        GenerateDungeon(); 

        Debug.Log($"Generated {rooms.Count} rooms and {corridors.Count} corridors");

        InstantiateRoomsAndCorridors();
        Destroy(GameObject.FindWithTag("Player"));
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

    void SpawnCheckpointManager()
    {
        if (checkpointManagerPrefab != null)
        {
            GameObject checkpointManagerObj = Instantiate(checkpointManagerPrefab, transform.position, Quaternion.identity);
            checkpointManagerObj.name = "CheckpointManager";
            checkpointManagerObj.transform.parent = transform;

            // Get the CheckpointManager component
            CheckpointManager checkpointManager = checkpointManagerObj.GetComponent<CheckpointManager>();

            if (checkpointManager != null)
            {
                // Now that it's instantiated, it will find the entry/exit tiles in its Start method
                Debug.Log("Checkpoint Manager spawned successfully");
            }
            else
            {
                Debug.LogError("CheckpointManager component not found on prefab!");
            }
        }
        else
        {
            Debug.LogWarning("No CheckpointManager prefab assigned to DungeonManager!");
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
                    case 4: Gizmos.color = Color.red; break;    // Exit
                    case 5: Gizmos.color = Color.magenta; break; // Shop
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
                
                // Draw entry and exit doorways
                if (room.type != 0) // If not spawn room
                {
                    Vector2 entryDoor = GetRoomDoorPosition(room, room.entryDir);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(ConvertToWorldPosition(entryDoor.x, entryDoor.y), 3f);
                }
                
                if (room.type != rooms.Count - 1) // If not exit room
                {
                    Vector2 exitDoor = GetRoomDoorPosition(room, room.exitDir);
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(ConvertToWorldPosition(exitDoor.x, exitDoor.y), 3f);
                }
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
package ecs

// ============================================================================
// MATH TYPES
//
// Shared value types used by the network layer for positional data. Kept in
// the ecs package so snapshots and world queries can pass coordinates around
// without importing another package.
// ============================================================================

// Vector3 is a 3-component position or direction.
type Vector3 struct{ X, Y, Z float32 }

// Quaternion is a rotation.
type Quaternion struct{ X, Y, Z, W float32 }
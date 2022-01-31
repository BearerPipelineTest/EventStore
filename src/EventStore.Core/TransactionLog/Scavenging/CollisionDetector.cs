﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.TransactionLog.Scavenging {
	// add things to the collision detector and it keeps a list of things that collided.
	public class CollisionDetector<T> {
		// checks if the hash is in use before this item at this position. returns true if so.
		// if returning true then out parameter is one of the items that hashes to that hash
		public delegate bool HashInUseBefore(T item, long itemPosition, out T candidateCollidee);

		private static EqualityComparer<T> TComparer { get; } = EqualityComparer<T>.Default;
		private readonly HashInUseBefore _hashInUseBefore;

		// store the values. could possibly store just the hashes instead but that would
		// lose us information and it should be so rare that there are any collisions at all.
		//qq for the real implementation make sure adding is idempotent
		private readonly HashSet<T> _collisions;
		
		//qq will need something like this to tell where to continue from. maybe not in this class though
		private long _lastPosition;

		public CollisionDetector(HashInUseBefore hashInUseBefore) {
			_collisions = new();
			_hashInUseBefore = hashInUseBefore;
		}

		public bool IsCollision(T item) => _collisions.Contains(item);
		public T[] GetAllCollisions() => _collisions.OrderBy(x => x).ToArray();

		// allllright. when we are adding an item, either:
		//   1. we have seen this stream before.
		//       then either:
		//       a. it was a collision the first time we saw it
		//           - by induction we concluded it was a collision at that time and added it to the
		//             collision list so we conclude it is a collision now
		//             TICK
		//       b. it was not a collision but has since been collided with by a newer stream.
		//           - when it was collided with we noticed and added it to the collision list so
		//             we conclude it is still a collision now
		//             TICK
		//             NB: it is important that we added this stream to the collision list. consider:
		//                 if we look in the index to see if there are any entries for this hash
		//                 there would be, because this is the collision case
		//                 and, crucially, those entries would be a mix of this stream and other streams
		//                 because this is the 'seen before' case.
		//                 so we couldn't tell by looking up a single entry whether this was a collision
		//                 or not. BAD.
		//       c. it was not a collision and still isn't
		//           - so we look in the index to see if there are any entries for this hash
		//             in the log _before_ this item.
		//             there are, because we have seen this stream before
		//             all the entries are for this stream, but we only want to check one.
		//             so we check one, and we find that it is for this stream
		//             conclude no collision
		//             TICK
		//
		//   2. this is the first time we are seeing this stream.
		//       then either:
		//       a. it collides with any stream we have seen before
		//           - so we look in the index to see if there are any entries for this hash
		//             in the log _before_ this item.
		//             there are, because this is the colliding case.
		//             we pick any entry and look up the record
		//             the record MUST be for a different stream because this is the the first time
		//                we are seeing this stream and we carefully excluded ourselves from the
		//                index search. (if we didn't do that then we could have found a record for
		//                our own stream here and not been able to distinguish this case from 1c
		//                based on looking up a single record.
		//             collision detected.
		//             add both streams to the collision list. we have their names and the hash.
		//             BUT is it possible that we are colliding with multiple streams, do we need to add
		//                    them all to the hash? it is possible yes, but they are already in the hash
		//                    because they are colliding with each other.
		//             TICK
		//       b. it does not collide with any stream we have seen before
		//           - so we look in the index to see if there are any entries for this hash
		//             there are not because this is the first time we are seeing this stream and it does
		//                not collde.
		//             conclude no collision
		//             TICK
		public void Add(T item, long itemPosition) {
			_lastPosition = itemPosition;

			if (IsCollision(item)) {
				return; // previously known collision. 1a or 1b.
			}

			// collision not previously known, but might be a new one now.
			if (!_hashInUseBefore(item, itemPosition, out var candidateCollidee)) {
				return; // hash not in use, can be no collision. 2b
			}

			// hash in use, but maybe by the item itself.
			if (TComparer.Equals(candidateCollidee, item)) {
				return; // no collision with oneself. 1c
			}

			// hash in use by a different item! found new collision. 2a
			_collisions.Add(item);
			_collisions.Add(candidateCollidee);
		}
	}
}

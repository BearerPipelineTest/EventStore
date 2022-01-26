﻿using System;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class InMemoryExecutor<TStreamId> : IExecutor<TStreamId> {
		private readonly IChunkManagerForScavenge _chunkManager;
		private readonly IChunkReaderForScavenge<TStreamId> _chunkReader;
		public InMemoryExecutor(
			IChunkManagerForScavenge chunkManager,
			IChunkReaderForScavenge<TStreamId> chunkReader) {

			_chunkManager = chunkManager;
			_chunkReader = chunkReader;
		}

		public void ExecuteChunks(IScavengeInstructions<TStreamId> instructions) {
			foreach (var chunkInstructions in instructions.ChunkInstructionss) {
				ExecuteChunk(instructions, chunkInstructions);
			}

		}

		public void ExecuteIndex(IScavengeInstructions<TStreamId> instructions) {
			//qq fill this in
		}

		private void ExecuteChunk(
			IScavengeInstructions<TStreamId> scavengeInstructions,
			IReadOnlyChunkScavengeInstructions<TStreamId> chunkInstructions) {

			var threshold = 1000; //qq make configurable
			if (chunkInstructions.NumRecordsToDiscard < threshold) {
				// they'll still (typically) be removed from the index
				return;
			}

			//qq the other reason we might want to not scanvenge this chunk is if the posmap would make it
			// bigger
			// than the original... limited concern because of the threshold above BUT we could address
			// by either
			//   - improving the posmap 
			//   - using a padding/scavengedevent system event to prevent having to write a posmap
			// this is the kind of decision we can make in here, local to the chunk.
			// knowing the numrecordstodiscard could be useful here, if we are just discarding a small
			// number then we'd probably pad them with gone events instead of adding a posmap.

			/* Set a scavenge point
			 * scavenge range or time
			 * keep scavenging until scavenge point is hit
			 *
			 * Setting the scavenge point has to be separate
			 * we can read the scavenge point stream to see if the scavenge data that we have is 
			 * consistent with that or we need to build/rebuild it
			 *
			 * - maybe in memory dictionary and flush to persistent storage per chunk?
			 * - per stream
			 *	- 0-1 * metadata (tb, maxcount, max age, metadata position)
			 * start the scavenge
			 *  - write down which scavenge point log position
			 *  - start gathering scavenge data up to that log position
			 *		- metadata gathered checkpoint
			 *  - start calculations for this log position
			 *		- calculated upto checkpoint
			 *  - could chase the calculated checkpoint if it only flushes when it has a full chunk's 
			 *  worth to keep after scavenging
			 */

			//			monday //qqq
			// 1. open the chunk, probably with the bulk reader

			//qq do this with a strategy so we can plug bulk reader in.
			
			//qq is the one in the chunkinstructions a logical chunk number or physical?
			// if physical, then we can get the physical chunk from the chunk manager and process it
			// if logical then bear in mind that the chunk we get from the chunk manager is the whole
			// physical file
			var chunk = _chunkManager.GetChunk(chunkInstructions.ChunkNumber);

			//qq var newChunk = ???;

			foreach (var record in _chunkReader.Read(chunk)) {
				//qq here we don't care about whether there were hash collisions or not
				// delegate that to the 'instructions'
				if (scavengeInstructions.TryGetDiscardPoint(record.StreamId, out var discardPoint) &&
					discardPoint.ShouldDiscard(record.EventNumber)) {

					//qq discard record
				} else {
					//qq keep record
					//qq do we need to upgrade it?
					//qq will using the bulk reader be awkward considering the record format
					// size changes that have occurred over the years
					// if so consider using the regular reader.
				}
			}
			// 2. read through it, keeping and discarding as necessary. probably no additional lookups at this point
			// 3. write the posmap
			// 4. finalise the chunk
			// 5. swap it in to the chunkmanager
			//qq _chunkManager.SwitchChunk();
		}
	}
}

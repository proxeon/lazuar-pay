#!/bin/bash
cd "$(dirname "$0")"

./00_provision.sh && \
./01_test_b2b.sh && \
./02_test_credit_note.sh && \
./03_test_b2c.sh

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 ALL LHDN SANDBOX TESTS PASSED SUCCESSFULLY!"
else
    echo ""
    echo "❌ LHDN SANDBOX TESTS FAILED."
fi

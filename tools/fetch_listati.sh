set -x
mkdir -p sources/listati && cd sources/listati
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36"
P=https://www.ilportaledellautomobilista.it
for u in "$P/web/portale-automobilista/foglio-rosa-e-prove-new" "$P/web/portale-automobilista/patenti-carta-qualificazione-conducente" "$P/web/portale-automobilista/-/quiz-per-le-patenti-am-b-superiori-e-cqc" "$P/web/portale-automobilista/quiz-patenti"; do
  n=$(echo "$u" | sed 's#.*/##; s/[^a-zA-Z0-9-]/_/g'); curl -sSL -A "$UA" --max-time 60 -o "page_$n.html" "$u"; done
grep -ohE 'href="[^"]*(documents|download)[^"]*"' page_*.html | sed 's/href="//;s/"$//' | sed 's/&amp;/\&/g' | sort -u > links.txt
cat links.txt
i=0
while read -r l; do
  case "$l" in http*) url="$l";; /*) url="$P$l";; *) continue;; esac
  if echo "$url" | grep -qiE 'superior|cqc|CQC|Poligrafico|33787|domande'; then
    i=$((i+1)); f=$(echo "$url" | sed 's#?.*##; s#.*/documents/[0-9]*/[0-9]*/##; s#/.*##' | sed 's/[^a-zA-Z0-9._+-]/_/g')
    curl -sSL -A "$UA" --max-time 120 -o "dl_${i}_${f:0:80}" "$url"
  fi
done < links.txt
ls -la

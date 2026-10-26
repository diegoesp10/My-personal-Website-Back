// Prepares the versioned content for publication: node pack-content.mjs <content folder> <output folder>.
// Only the language files (portfolio.json, portfolio.<language>.json) and the images saved by the editor
// (uploads/<32 hex>.png|jpg|webp) are copied; anything else stays out. Every image referenced by the content must exist.
// The revision of each language becomes a hash of its content, so a change always reaches browsers (it is the ETag)
// and publishing the same content twice writes identical files.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const [source, target] = process.argv.slice(2);
if (!source || !target) fail('Usage: node pack-content.mjs <content folder> <output folder>');

const languageFile = /^portfolio(\.[a-z]{2})?\.json$/;
const imageFile = /^[a-f0-9]{32}\.(png|jpg|webp)$/;
// Only names the editor can produce; a text that merely mentions /uploads is not a reference.
const imageReference = /\/uploads\/([a-f0-9]{32}\.(?:png|jpg|webp))/g;

function fail(message) {
  console.error('Content error: ' + message);
  process.exit(1);
}

if (!fs.statSync(source, { throwIfNoEntry: false })?.isDirectory()) fail(`${source} is not a folder.`);
if (fs.existsSync(target) && fs.readdirSync(target).length) fail(`${target} must be empty.`);
fs.mkdirSync(path.join(target, 'uploads'), { recursive: true });

const uploads = path.join(source, 'uploads');
const images = new Set(
  fs.existsSync(uploads)
    ? fs.readdirSync(uploads, { withFileTypes: true })
        .filter((entry) => entry.isFile() && imageFile.test(entry.name))
        .map((entry) => entry.name)
    : [],
);

const languages = fs.readdirSync(source, { withFileTypes: true })
  .filter((entry) => entry.isFile() && languageFile.test(entry.name))
  .map((entry) => entry.name)
  .sort();
if (!languages.includes('portfolio.json')) fail('portfolio.json (Spanish) is missing.');

const referenced = new Set();
for (const name of languages) {
  let content;
  try {
    content = JSON.parse(fs.readFileSync(path.join(source, name), 'utf8'));
  } catch (error) {
    fail(`${name} is not valid JSON: ${error.message}`);
  }
  if (!content || typeof content !== 'object' || Array.isArray(content)) fail(`${name} is not a content object.`);

  const { revision: _previous, ...body } = content;
  const revision = crypto.createHash('sha256').update(JSON.stringify(body)).digest('hex').slice(0, 32);
  const published = JSON.stringify({ ...content, revision }, null, 2) + '\n';
  for (const [, image] of published.matchAll(imageReference)) {
    if (!images.has(image)) fail(`${name} references /uploads/${image}, which is not in ${uploads}.`);
    referenced.add(image);
  }
  fs.writeFileSync(path.join(target, name), published);
  console.log(`${name}: revision ${revision}`);
}

// Images that no language references any more are still published: older backups may point to them.
for (const image of images) fs.copyFileSync(path.join(uploads, image), path.join(target, 'uploads', image));
console.log(`${images.size} images (${referenced.size} referenced).`);

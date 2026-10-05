import yaml, os, glob

d = yaml.safe_load(open(r'G:\megame\.github\workflows\ci.yml', encoding='utf-8'))
wr = d['jobs']['windows-installer']
tmp = os.environ['TEMP']
for f in glob.glob(os.path.join(tmp, 'st*.ps1')):
    os.remove(f)

i = 0
for s in wr['steps']:
    if 'run' in s and s.get('shell') == 'pwsh':
        code = s['run'].replace('${{ github.ref_name }}', 'v1.0.0')
        p = os.path.join(tmp, 'st%d.ps1' % i)
        open(p, 'w', encoding='utf-8').write(code)
        print('wrote %s  <- %s' % (p, s['name']))
        i += 1